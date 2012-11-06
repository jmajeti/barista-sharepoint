﻿namespace Barista.SharePoint.OrcaDB
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.Net;
  using System.Threading.Tasks;
  using System.Web;
  using CamlexNET;
  using Microsoft.Office.DocumentManagement.DocumentSets;
  using Microsoft.Office.Server.Utilities;
  using Microsoft.SharePoint;
  using Barista.OrcaDB;

  /// <summary>
  /// Represents a SharePoint-backed Document Store.
  /// </summary>
  public class SPDocumentStore :
    IFullyCapableDocumentStore,
    IAsyncExecDocumentStore,
    IDisposable
  {
    #region Fields
    /// <summary>
    /// Holds the original value of the access denied value from SPSecurity.CatchAccessDeniedException
    /// </summary>
    private readonly bool m_originalCatchAccessDeniedValue;

    /// <summary>
    /// The Id of SPLocks created by the document store.
    /// </summary>
    private const string OrcaDBEntityLockId = "OrcaDB Entity Lock";
    #endregion

    #region Construtors
    /// <summary>
    /// Initializes a new instance of the <see cref="SPDocumentStore"/> class.
    /// </summary>
    public SPDocumentStore()
    {
      if (SPContext.Current == null || SPContext.Current.Web == null)
        throw new InvalidOperationException("A current SPContext must be available.");

      this.Web = SPContext.Current.Web;
      m_originalCatchAccessDeniedValue = SPSecurity.CatchAccessDeniedException;
      SPSecurity.CatchAccessDeniedException = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SPDocumentStore"/> class.
    /// </summary>
    /// <param name="web">The web.</param>
    public SPDocumentStore(SPWeb web)
    {
      if (web == null)
        throw new ArgumentNullException("web");

      this.Web = web;
      m_originalCatchAccessDeniedValue = SPSecurity.CatchAccessDeniedException;
      SPSecurity.CatchAccessDeniedException = false;
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets the SPWeb that is associated with the document store. Always use this value instead of SPContext.Current.Web.
    /// </summary>
    public SPWeb Web
    {
      get;
      private set;
    }
    #endregion

    #region Containers

    /// <summary>
    /// Creates a container in the document store with the specified title and description.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="description">The description.</param>
    /// <returns></returns>
    public virtual Container CreateContainer(string containerTitle, string description)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          web.AllowUnsafeUpdates = true;
          try
          {
            Guid listID = web.Lists.Add(containerTitle,                         //List Title
                                        description,                            //List Description
                                        "Lists/" + containerTitle,              //List Url
                                        "0984cbdd-a989-422e-9fe6-08a980a669a6", //Feature Id of List definition Provisioning Feature – CustomList Feature Id
                                        10080,                                  //List Template Type
                                        "121");                                 //Document Template Type .. 101 is for None
            return SPDocumentStoreHelper.MapContainerFromSPList(web.Lists[listID]);
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }
        }
      }
    }

    /// <summary>
    /// Gets the container with the specified title from the document store.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <returns></returns>
    public virtual Container GetContainer(string containerTitle)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          return SPDocumentStoreHelper.MapContainerFromSPList(list);
        }
      }
    }

    /// <summary>
    /// Updates the container in the document store with new values.
    /// </summary>
    /// <param name="container">The container.</param>
    /// <returns></returns>
    public virtual bool UpdateContainer(Container container)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, container.Title, out list, out folder, String.Empty) == false)
            return false;

          web.AllowUnsafeUpdates = true;
          try
          {
            list.Title = container.Title;
            list.Description = container.Description;
            list.Update();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }
          return true;
        }
      }
    }

    /// <summary>
    /// Deletes the container with the specified title from the document store.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    public virtual void DeleteContainer(string containerTitle)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return;

          web.AllowUnsafeUpdates = true;
          try
          {
            list.Delete();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }
        }
      }
    }

    /// <summary>
    /// Lists all containers contained in the document store.
    /// </summary>
    /// <returns></returns>
    public virtual IList<Container> ListContainers()
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          List<Container> result = new List<Container>();
          foreach (var list in SPDocumentStoreHelper.GetContainers(web))
          {
            result.Add(SPDocumentStoreHelper.MapContainerFromSPList(list));
          }
          return result;
        }
      }
    }
    #endregion

    #region Folders

    /// <summary>
    /// Creates the folder with the specified path in the container.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <returns></returns>
    public virtual Folder CreateFolder(string containerTitle, string path)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          var currentFolder = CreateFolderInternal(web, containerTitle, path);

          return SPDocumentStoreHelper.MapFolderFromSPFolder(currentFolder);
        }
      }
    }

    internal SPFolder CreateFolderInternal(SPWeb web, string containerTitle, string path)
    {
      SPList list = web.Lists.TryGetList(containerTitle);
      if ((list == null) || (list.TemplateFeatureId != Constants.DocumentContainerFeatureId))
      {
        throw new InvalidOperationException("A container with the specified title does not exist: " + web.Url + " " + containerTitle);
      }

      SPFolder currentFolder = list.RootFolder;
      web.AllowUnsafeUpdates = true;

      try
      {
        foreach (string subFolder in DocumentStoreHelper.GetPathSegments(path))
        {
          if (currentFolder.SubFolders.OfType<SPFolder>().Any(sf => sf.Name == subFolder) == false)
          {
            SPFolder folder = currentFolder.SubFolders.Add(subFolder);

            //Ensure that the content type is, well, a folder and not a doc set. :-0
            var folderListContentType = list.ContentTypes.BestMatch(SPBuiltInContentTypeId.Folder);
            if (folder.Item.ContentTypeId != folderListContentType)
            {
              folder.Item["ContentTypeId"] = folderListContentType;
              folder.Item.Update();
            }
          }
          currentFolder = currentFolder.SubFolders[subFolder];
        }
      }
      finally
      {
        web.AllowUnsafeUpdates = false;
      }

      return currentFolder;
    }

    /// <summary>
    /// Gets the folder with the specified path that is contained in the container.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <returns></returns>
    public virtual Folder GetFolder(string containerTitle, string path)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, path) == false)
            return null;

          return SPDocumentStoreHelper.MapFolderFromSPFolder(folder);
        }
      }
    }

    /// <summary>
    /// Renames the specified folder to the new folder name.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <param name="newFolderName">New name of the folder.</param>
    /// <returns></returns>
    public virtual Folder RenameFolder(string containerTitle, string path, string newFolderName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, path) == false)
            return null;

          web.AllowUnsafeUpdates = true;
          try
          {
            folder.Item["Name"] = newFolderName;
            folder.Item.Update();

            folder = list.GetItemByUniqueId(folder.UniqueId).Folder;

            return SPDocumentStoreHelper.MapFolderFromSPFolder(folder);
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }
        }
      }
    }

    /// <summary>
    /// Deletes the folder with the specified path from the container.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    public virtual void DeleteFolder(string containerTitle, string path)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, path) == false)
            return;

          web.AllowUnsafeUpdates = true;
          try
          {
            folder.Delete();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }
        }
      }
    }

    /// <summary>
    /// Lists the folders at the specified level in the container.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <returns></returns>
    public virtual IList<Folder> ListFolders(string containerTitle, string path)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {

          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, path) == false)
            return null;

          List<Folder> result = new List<Folder>();

          var folderContentTypeId = list.ContentTypes.BestMatch(SPBuiltInContentTypeId.Folder);

          ContentIterator itemsIterator = new ContentIterator();
          itemsIterator.ProcessItemsInFolder(list, folder, false, true, false, (spListItem) =>
          {
            if (spListItem.ContentTypeId == folderContentTypeId)
              result.Add(SPDocumentStoreHelper.MapFolderFromSPFolder(spListItem.Folder));
          }, null);

          return result;
        }
      }
    }

    /// <summary>
    /// Lists all folders contained in the specified container.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <returns></returns>
    public virtual IList<Folder> ListAllFolders(string containerTitle, string path)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, path) == false)
            return null;

          List<Folder> result = new List<Folder>();

          var folderContentTypeId = list.ContentTypes.BestMatch(SPBuiltInContentTypeId.Folder);

          ContentIterator itemsIterator = new ContentIterator();
          itemsIterator.ProcessItemsInFolder(list, folder, true, true, false, (spListItem) =>
          {
            if (spListItem.ContentTypeId == folderContentTypeId)
              result.Add(SPDocumentStoreHelper.MapFolderFromSPFolder(spListItem.Folder));
          }, null);

          return result;
        }
      }
    }
    #endregion

    #region Entities

    /// <summary>
    /// Creates strongly typed entity in the specified container with the specified namespace and value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="namespace">The @namespace.</param>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public Entity<T> CreateEntity<T>(string containerTitle, string @namespace, T value)
    {
      return CreateEntity<T>(containerTitle, String.Empty, @namespace, value);
    }

    /// <summary>
    /// Creates a new entity in the document store, contained in the specified container in the specified namespace.
    /// </summary>
    /// <param name="containerTitle">The container title. Required.</param>
    /// <param name="namespace">The namespace of the entity. Optional.</param>
    /// <param name="data">The data to store with the entity. Optional.</param>
    /// <returns></returns>
    public Entity CreateEntity(string containerTitle, string @namespace, string data)
    {
      return CreateEntity(containerTitle, String.Empty, @namespace, data);
    }

    /// <summary>
    /// Creates the entity.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <param name="namespace">The @namespace.</param>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public Entity<T> CreateEntity<T>(string containerTitle, string path, string @namespace, T value)
    {
      var data = DocumentStoreHelper.SerializeObjectToJson<T>(value);
      var result = CreateEntity(containerTitle, path, @namespace, data);
      return new Entity<T>(result);
    }

    /// <summary>
    /// Creates a new entity in the document store, contained in the specified container in the specified folder and namespace.
    /// </summary>
    /// <param name="containerTitle">The container title. Required.</param>
    /// <param name="path">The path. Optional.</param>
    /// <param name="namespace">The namespace of the entity. Optional.</param>
    /// <param name="data">The data to store with the entity. Optiona.</param>
    /// <returns></returns>
    public virtual Entity CreateEntity(string containerTitle, string path, string @namespace, string data)
    {
      if (data == null)
        data = String.Empty;

      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, path) == false)
            throw new InvalidOperationException("Unable to retrieve the specified folder -- Folder does not exist.");

          Guid newGuid = Guid.NewGuid();
          var docEntityContentTypeId = list.ContentTypes.BestMatch(new SPContentTypeId(Constants.DocumentStoreEntityContentTypeId));

          Hashtable properties = new Hashtable();
          properties.Add("DocumentSetDescription", "Document Store Entity");
          properties.Add("DocumentEntityGuid", newGuid.ToString());
          properties.Add("Namespace", @namespace);

          DocumentSet newDocumentSet;

          web.AllowUnsafeUpdates = true;
          try
          {
            if ((folder.Item == null && list.RootFolder == folder && list.DoesUserHavePermissions(SPBasePermissions.AddListItems) == false) ||
                (folder.Item != null && (folder.Item.DoesUserHavePermissions(SPBasePermissions.AddListItems) == false)))
              throw new InvalidOperationException("Insufficent Permissions.");

            if (PermissionsHelper.IsRunningUnderElevatedPrivledges(site.WebApplication.ApplicationPool))
            {
              newDocumentSet = DocumentSet.Create(folder, newGuid.ToString(), docEntityContentTypeId, properties, true, this.Web.CurrentUser);

              newDocumentSet.Folder.Files.Add(newDocumentSet.Folder.Url + "/" + Constants.DocumentStoreDefaultEntityPartFileName, System.Text.UTF8Encoding.Default.GetBytes(data), null, this.Web.CurrentUser, this.Web.CurrentUser, DateTime.UtcNow, DateTime.UtcNow, true);

              //Set the created/updated fields of the new document set to the current user.
              string userLogonName = this.Web.CurrentUser.ID + ";#" + this.Web.CurrentUser.Name;
              newDocumentSet.Folder.Item[SPBuiltInFieldId.Editor] = userLogonName;
              newDocumentSet.Folder.Item.UpdateOverwriteVersion();
            }
            else
            {
              newDocumentSet = DocumentSet.Create(folder, newGuid.ToString(), docEntityContentTypeId, properties, true);

              newDocumentSet.Folder.Files.Add(newDocumentSet.Folder.Url + "/" + Constants.DocumentStoreDefaultEntityPartFileName, System.Text.UTF8Encoding.Default.GetBytes(data), true);
            }
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return SPDocumentStoreHelper.MapEntityFromSPListItem(newDocumentSet.Item);
        }
      }
    }

    /// <summary>
    /// Gets the strongly-typed entity from the specified container with the specified id.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <returns></returns>
    public Entity<T> GetEntity<T>(string containerTitle, Guid entityId)
    {
      var result = GetEntity(containerTitle, entityId);

      if (result == null)
        return null;

      return new Entity<T>(result);
    }

    /// <summary>
    /// Gets the specified untyped entity.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <returns></returns>
    public Entity GetEntity(string containerTitle, Guid entityId)
    {
      return GetEntity(containerTitle, entityId, String.Empty);
    }

    /// <summary>
    /// Gets the specifed strongly-typed entity in the specified path.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="path">The path.</param>
    /// <returns></returns>
    public Entity<T> GetEntity<T>(string containerTitle, Guid entityId, string path)
    {
      var result = GetEntity(containerTitle, entityId, path);

      if (result == null)
        return null;

      return new Entity<T>(result);
    }

    /// <summary>
    /// Gets the specified untyped entity in the specified path.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="path"></param>
    /// <returns></returns>
    public virtual Entity GetEntity(string containerTitle, Guid entityId, string path)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, path) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          var entity = SPDocumentStoreHelper.MapEntityFromSPListItem(defaultEntityPart.ParentFolder.Item);
          ProcessEntityFile(containerTitle, entityId, defaultEntityPart, entity);

          return entity;
        }
      }
    }

    /// <summary>
    /// Gets the ETag of the specified Entities' contents.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <returns></returns>
    public virtual string GetEntityContentsETag(string containerTitle, Guid entityId)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          var combinedETag = String.Join(", ", defaultEntityPart.ParentFolder.Files.OfType<SPFile>().Select(f => f.ETag).ToArray());

          return StringHelper.CreateMD5Hash(combinedETag);
        }
      }
    }

    /// <summary>
    /// When overridden in a subclass, allows custom processing to occur on the SPFile/Entity that is retrieved from SharePoint.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="entityFile">The entity file.</param>
    /// <param name="entity">The entity.</param>
    protected virtual void ProcessEntityFile(string containerTitle, Guid entityId, SPFile entityFile, Entity entity)
    {
      //Does nothing in the base implementation.
    }

    /// <summary>
    /// Updates the entity.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entity">The entity.</param>
    /// <returns></returns>
    public virtual bool UpdateEntity(string containerTitle, Entity entity)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entity.Id, out defaultEntityPart) == false)
            return false;

          if (defaultEntityPart.Item.DoesUserHavePermissions(SPBasePermissions.EditListItems) == false)
            throw new InvalidOperationException("Insufficent Permissions.");

          if (defaultEntityPart.ETag != entity.ETag)
          {
            throw new InvalidOperationException("Could not update the entity, the Entity has been updated by another user.");
          }

          web.AllowUnsafeUpdates = true;
          try
          {
            string currentData = String.Empty;
            currentData = defaultEntityPart.Web.GetFileAsString(defaultEntityPart.Url);

            if (entity.Data != currentData)
            {
              if (String.IsNullOrEmpty(entity.Data) == false)
              {
                defaultEntityPart.SaveBinary(System.Text.UTF8Encoding.Default.GetBytes(entity.Data));
              }
              else
              {
                defaultEntityPart.SaveBinary(System.Text.UTF8Encoding.Default.GetBytes(String.Empty));
              }
            }

            DocumentSet ds = DocumentSet.GetDocumentSet(defaultEntityPart.ParentFolder);
            if (ds.Item.Title != entity.Title)
              ds.Item["Title"] = entity.Title;

            if ((ds.Item["DocumentSetDescription"] as string) != entity.Description)
              ds.Item["DocumentSetDescription"] = entity.Description;

            if ((ds.Item["Namespace"] as string) != entity.Namespace)
              ds.Item["Namespace"] = entity.Namespace;

            ds.Item.SystemUpdate(true);
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return true;
        }
      }
    }

    /// <summary>
    /// Updates the entity.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public virtual bool UpdateEntity<T>(string containerTitle, Guid entityId, T value)
    {
      string data = DocumentStoreHelper.SerializeObjectToJson<T>(value);

      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return false;

          if (defaultEntityPart.Item.DoesUserHavePermissions(SPBasePermissions.EditListItems) == false)
            throw new InvalidOperationException("Insufficent Permissions.");

          web.AllowUnsafeUpdates = true;
          try
          {
            string currentData = String.Empty;
            currentData = defaultEntityPart.Web.GetFileAsString(defaultEntityPart.Url);

            if (data != currentData)
            {
              if (String.IsNullOrEmpty(data) == false)
              {
                defaultEntityPart.SaveBinary(System.Text.UTF8Encoding.Default.GetBytes(data));
              }
              else
              {
                defaultEntityPart.SaveBinary(System.Text.UTF8Encoding.Default.GetBytes(String.Empty));
              }
            }
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return true;
        }
      }
    }

    /// <summary>
    /// Deletes the specified entity from the specified container.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <returns></returns>
    public virtual bool DeleteEntity(string containerTitle, Guid entityId)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          web.AllowUnsafeUpdates = true;
          try
          {
            SPFile dataFile = SPDocumentStoreHelper.GetDocumentStoreDefaultEntityPartForGuid(list, list.RootFolder, entityId);
            if (dataFile == null)
              return false;

            dataFile.ParentFolder.Delete();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return true;
        }
      }
    }

    /// <summary>
    /// Lists the entities.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="containerTitle">The container title.</param>
    /// <returns></returns>
    public IList<Entity<T>> ListEntities<T>(string containerTitle)
    {
      List<Entity<T>> results = new List<Entity<T>>();
      foreach (var entity in ListEntities(containerTitle, String.Empty, null))
      {
        results.Add(new Entity<T>(entity));
      }
      return results;
    }

    /// <summary>
    /// Lists the entities.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <returns></returns>
    public IList<Entity> ListEntities(string containerTitle)
    {
      return ListEntities(containerTitle, String.Empty, null);
    }

    /// <summary>
    /// Lists the entities.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <returns></returns>
    public IList<Entity> ListEntities(string containerTitle, string path)
    {
      return ListEntities(containerTitle, path, null);
    }

    /// <summary>
    /// Lists the entities.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="namespace">The @namespace.</param>
    /// <returns></returns>
    public IList<Entity<T>> ListEntities<T>(string containerTitle, string @namespace)
    {
      List<Entity<T>> results = new List<Entity<T>>();
      foreach (var entity in ListEntities(containerTitle, String.Empty, null))
      {
        results.Add(new Entity<T>(entity));
      }
      return results;
    }

    /// <summary>
    /// Lists the entities.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="criteria">The criteria.</param>
    /// <returns></returns>
    public IList<Entity<T>> ListEntities<T>(string containerTitle, EntityFilterCriteria criteria)
    {
      return ListEntities<T>(containerTitle, String.Empty, criteria);
    }

    /// <summary>
    /// Lists the entities.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <param name="criteria">The criteria.</param>
    /// <returns></returns>
    public IList<Entity<T>> ListEntities<T>(string containerTitle, string path, EntityFilterCriteria criteria)
    {
      List<Entity<T>> results = new List<Entity<T>>();
      foreach (var entity in ListEntities(containerTitle, path, criteria))
      {
        results.Add(new Entity<T>(entity));
      }
      return results;
    }

    /// <summary>
    /// Lists all entities contained in the container with the specified namespace.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public IList<Entity> ListEntities(string containerTitle, EntityFilterCriteria criteria)
    {
      return ListEntities(containerTitle, String.Empty, criteria);
    }

    /// <summary>
    /// Lists the entities.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <param name="criteria">The criteria.</param>
    /// <returns></returns>
    public virtual IList<Entity> ListEntities(string containerTitle, string path, EntityFilterCriteria criteria)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, path) == false)
            return null;

          List<Entity> result = new List<Entity>();

          if (folder.ItemCount == 0)
            return result;

          string queryString = Camlex.Query()
              .Where(li => ((string)li[SPBuiltInFieldId.ContentTypeId])
              .StartsWith(Constants.DocumentStoreEntityContentTypeId.ToLowerInvariant()))
              .ToString();

          if (criteria != null)
          {
            if (String.IsNullOrEmpty(criteria.Namespace) == false)
              queryString = Camlex.Query()
                  .Where(li => ((string)li[SPBuiltInFieldId.ContentTypeId])
                  .StartsWith(Constants.DocumentStoreEntityContentTypeId.ToLowerInvariant()) && ((string)li[Constants.NamespaceFieldId]) == criteria.Namespace)
                  .ToString();
          }

          SPQuery query = new SPQuery();
          query.Query = queryString;
          query.ViewFields = Camlex.Query().ViewFields(new List<Guid>() { 
            SPBuiltInFieldId.ID, 
            SPBuiltInFieldId.Title, 
            SPBuiltInFieldId.FileRef, 
            SPBuiltInFieldId.FileDirRef, 
            SPBuiltInFieldId.FileLeafRef, 
            SPBuiltInFieldId.FSObjType, 
            SPBuiltInFieldId.ContentTypeId, 
            SPBuiltInFieldId.Created,
            SPBuiltInFieldId.Modified,
            SPBuiltInFieldId.Author,
            SPBuiltInFieldId.Editor,
            Constants.DocumentEntityGuidFieldId,
            Constants.NamespaceFieldId,
            new Guid("CBB92DA4-FD46-4C7D-AF6C-3128C2A5576E") // DocumentSetDescription
          });
          query.ViewFieldsOnly = true;
          query.QueryThrottleMode = SPQueryThrottleOption.Override;
          query.Folder = folder;
          query.ViewAttributes = "Scope=\"Recursive\"";

          if (criteria != null)
          {
            if (criteria.Skip.HasValue)
            {
              SPListItemCollectionPosition itemPosition = new SPListItemCollectionPosition(String.Format("Paged=TRUE&p_ID={0}", criteria.Skip.Value));
            }

            if (criteria.Top.HasValue)
              query.RowLimit = criteria.Top.Value;
          }


          //Why doesn't the ContentIterator work? Why does it not iterate through? I have no idea...
          //ContentIterator itemsIterator = new ContentIterator();
          //itemsIterator.ProcessListItems(list, query, false, (spListItem) =>
          //{
          //  var entity = SPDocumentStoreHelper.MapEntityFromSPListItem(spListItem);
          //  result.Add(entity);
          //},
          //(spListItem, ex) =>
          //{
          //  return true;
          //});

          var listItems = list.GetItems(query).OfType<SPListItem>();
          foreach (var spListItem in listItems)
          {
            var entity = SPDocumentStoreHelper.MapEntityFromSPListItem(spListItem);
            if (entity != null)
              result.Add(entity);
          }

          ProcessEntityList(containerTitle, path, criteria, folder, result);
          return result;
        }
      }
    }

    /// <summary>
    /// Gets the ETag for the folder.
    /// </summary>
    /// <param name="containerTitle"></param>
    /// <param name="path"></param>
    /// <param name="criteria"></param>
    /// <returns></returns>
    public virtual string GetFolderETag(string containerTitle, string path, EntityFilterCriteria criteria)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, path) == false)
            return null;

          List<Entity> result = new List<Entity>();

          if (folder.ItemCount == 0)
            return StringHelper.CreateMD5Hash(folder.UniqueId.ToString() + ((DateTime)folder.Properties["vti_timelastmodified"]).Ticks.ToString());

          string queryString = Camlex.Query()
              .Where(li => ((string)li[SPBuiltInFieldId.ContentTypeId])
              .StartsWith(Constants.DocumentStoreEntityContentTypeId.ToLowerInvariant()))
              .OrderBy(li => li[SPBuiltInFieldId.Last_x0020_Modified] as Camlex.Desc)
              .ToString();

          if (criteria != null)
          {
            if (String.IsNullOrEmpty(criteria.Namespace) == false)
              queryString = Camlex.Query()
                  .Where(li => ((string)li[SPBuiltInFieldId.ContentTypeId])
                  .StartsWith(Constants.DocumentStoreEntityContentTypeId.ToLowerInvariant()) && ((string)li[Constants.NamespaceFieldId]) == criteria.Namespace)
                  .OrderBy(li => li[SPBuiltInFieldId.Last_x0020_Modified] as Camlex.Desc)
                  .ToString();
          }

          SPQuery query = new SPQuery();
          query.Query = queryString;
          query.ViewFields = Camlex.Query().ViewFields(new List<Guid>() { 
            SPBuiltInFieldId.ID,
            SPBuiltInFieldId.Title,
            SPBuiltInFieldId.FileRef, 
            SPBuiltInFieldId.FileDirRef, 
            SPBuiltInFieldId.FileLeafRef, 
            SPBuiltInFieldId.FSObjType,
            SPBuiltInFieldId.ContentTypeId, 
            Constants.DocumentEntityGuidFieldId,
            Constants.NamespaceFieldId,
          });
          query.ViewFieldsOnly = true;
          query.QueryThrottleMode = SPQueryThrottleOption.Override;
          query.Folder = folder;
          query.ViewAttributes = "Scope=\"Recursive\"";

          var items = list.GetItems(query).OfType<SPListItem>();

          if (items.Any() == false)
            return StringHelper.CreateMD5Hash(folder.UniqueId.ToString() + ((DateTime)folder.Properties["vti_timelastmodified"]).Ticks.ToString());

          var tags = new List<string>();
          foreach(var item in items)
          {
            Guid id = new Guid(item.Name);
            tags.Add(GetEntityContentsETag(containerTitle, id));
          }
          
          var combinedETag = String.Join(", ", tags.ToArray());

          return StringHelper.CreateMD5Hash(combinedETag);
        }
      }
    }

    /// <summary>
    /// When overridden in a subclass, allows custom processing to occur on the SPListItemCollection/IList<Entity> that is retrieved from SharePoint.
    /// </summary>
    /// <param name="containerTitle"></param>
    /// <param name="entityId"></param>
    /// <param name="entityFile"></param>
    /// <param name="entity"></param>
    protected virtual void ProcessEntityList(string containerTitle, string path, EntityFilterCriteria criteria, SPFolder folder, IList<Entity> entities)
    {
      //Does nothing in the base implementation.
    }

    /// <summary>
    /// Imports an entity from a previous export.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="namespace">The @namespace.</param>
    /// <param name="archiveData">The archive data.</param>
    /// <returns></returns>
    public Entity ImportEntity(string containerTitle, Guid entityId, string @namespace, byte[] archiveData)
    {
      return ImportEntity(containerTitle, String.Empty, entityId, @namespace, archiveData);
    }

    /// <summary>
    /// Imports an entity from a previous export.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="namespace">The @namespace.</param>
    /// <param name="archiveData">The archive data.</param>
    /// <returns></returns>
    public virtual Entity ImportEntity(string containerTitle, string path, Guid entityId, string @namespace, byte[] archiveData)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, path) == false)
          {
            folder = CreateFolderInternal(web, containerTitle, path);
          }

          var docEntityContentTypeId = list.ContentTypes.BestMatch(new SPContentTypeId(Constants.DocumentStoreEntityContentTypeId));

          Hashtable properties = new Hashtable();
          properties.Add("DocumentSetDescription", "Document Store Entity");
          properties.Add("DocumentEntityGuid", entityId.ToString());
          properties.Add("Namespace", @namespace);

          DocumentSet importedDocSet = null;

          web.AllowUnsafeUpdates = true;
          try
          {
            importedDocSet = DocumentSet.Import(archiveData, entityId.ToString(), folder, docEntityContentTypeId, properties, this.Web.CurrentUser);
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return SPDocumentStoreHelper.MapEntityFromSPListItem(importedDocSet.Item);
        }
      }
    }

    /// <summary>
    /// Returns a stream that contains an export of the entity.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <returns></returns>
    public virtual Stream ExportEntity(string containerTitle, Guid entityId)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          DocumentSet docSet = DocumentSet.GetDocumentSet(defaultEntityPart.ParentFolder);

          MemoryStream ms = new MemoryStream();
          docSet.Export(ms);
          ms.Flush();
          ms.Seek(0, SeekOrigin.Begin);
          return ms;
        }
      }
    }

    /// <summary>
    /// Moves the specified entity to the specified destination folder.
    /// </summary>
    /// <param name="containerTitle"></param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="destinationPath">The destination path.</param>
    /// <returns></returns>
    public virtual bool MoveEntity(string containerTitle, Guid entityId, string destinationPath)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            throw new InvalidOperationException("A container with the specified title could not be found.");

          SPList destinationList; // Should be the same....
          SPFolder destinationFolder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out destinationList, out destinationFolder, destinationPath) == false)
            throw new InvalidOperationException("The destination folder is invalid.");

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            throw new InvalidOperationException("An entity with the specified id could not be found.");

          web.AllowUnsafeUpdates = true;
          try
          {
            defaultEntityPart.ParentFolder.MoveTo(destinationFolder.Url + "/" + defaultEntityPart.ParentFolder.Name);
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }
          return true;
        }
      }
    }
    #endregion

    #region EntityParts

    /// <summary>
    /// Creates the entity part.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public EntityPart<T> CreateEntityPart<T>(string containerTitle, Guid entityId, string partName, T value)
    {
      return CreateEntityPart<T>(containerTitle, entityId, partName, String.Empty, value);
    }

    public EntityPart<T> CreateEntityPart<T>(string containerTitle, Guid entityId, string partName, string category, T value)
    {
      string data = DocumentStoreHelper.SerializeObjectToJson<T>(value);
      var result = CreateEntityPart(containerTitle, entityId, partName, category, data);
      return new EntityPart<T>(result);
    }

    /// <summary>
    /// Creates the entity part.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <param name="data">The data.</param>
    /// <returns></returns>
    public EntityPart CreateEntityPart(string containerTitle, Guid entityId, string partName, string data)
    {
      return CreateEntityPart(containerTitle, entityId, partName, String.Empty, data);
    }

    /// <summary>
    /// Creates the entity part.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <param name="category">The category.</param>
    /// <param name="data">The data.</param>
    /// <returns></returns>
    public virtual EntityPart CreateEntityPart(string containerTitle, Guid entityId, string partName, string category, string data)
    {
      if (partName + Constants.DocumentSetEntityPartExtension == Constants.DocumentStoreDefaultEntityPartFileName)
        throw new InvalidOperationException("Filename is reserved.");

      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          var entityPartContentType = list.ContentTypes.OfType<SPContentType>().Where(ct => ct.Id.ToString().ToLowerInvariant().StartsWith(Constants.DocumentStoreEntityPartContentTypeId.ToLowerInvariant())).FirstOrDefault();

          Hashtable properties = new Hashtable();
          properties.Add("ContentTypeId", entityPartContentType.Id.ToString());
          properties.Add("Content Type", entityPartContentType.Name);

          if (String.IsNullOrEmpty(category) == false)
            properties.Add("Category", category);

          web.AllowUnsafeUpdates = true;
          try
          {
            if (defaultEntityPart.ParentFolder.Item.DoesUserHavePermissions(SPBasePermissions.AddListItems) == false)
              throw new InvalidOperationException("Insufficent Permissions.");

            SPFile partFile = defaultEntityPart.ParentFolder.Files.Add(partName + Constants.DocumentSetEntityPartExtension, System.Text.UTF8Encoding.Default.GetBytes(data), properties, true);
            return SPDocumentStoreHelper.MapEntityPartFromSPFile(partFile);
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }
        }
      }
    }

    /// <summary>
    /// Gets the entity part.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <returns></returns>
    public virtual EntityPart GetEntityPart(string containerTitle, Guid entityId, string partName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, entityId, partName, out entityPart) == false)
            return null;

          var result = SPDocumentStoreHelper.MapEntityPartFromSPFile(entityPart);
          ProcessEntityPartFile(containerTitle, entityId, partName, entityPart, result);

          return result;
        }
      }
    }

    /// <summary>
    /// Gets the ETag of the specified entity part
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName"></param>
    /// <returns></returns>
    public virtual string GetEntityPartETag(string containerTitle, Guid entityId, string partName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, entityId, partName, out entityPart) == false)
            return null;

          var result = entityPart.ETag;
          return result;
        }
      }
    }

    /// <summary>
    /// When overridden in a subclass, allows custom processing to occur on the SPFile/EntityPart that is retrieved from SharePoint.
    /// </summary>
    /// <param name="containerTitle"></param>
    /// <param name="entityId"></param>
    /// <param name="entityFile"></param>
    /// <param name="entity"></param>
    protected virtual void ProcessEntityPartFile(string containerTitle, Guid entityId, string partName, SPFile entityPartFile, EntityPart entity)
    {
      //Does nothing in the base implementation.
    }

    /// <summary>
    /// Tries the get entity part.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <param name="entityPart">The entity part.</param>
    /// <returns></returns>
    public bool TryGetEntityPart<T>(string containerTitle, Guid entityId, string partName, out EntityPart<T> entityPart)
    {
      entityPart = null;
      try
      {
        EntityPart untypedEntityPart = GetEntityPart(containerTitle, entityId, partName);
        if (untypedEntityPart == null)
          return false;

        entityPart = new EntityPart<T>(untypedEntityPart);
      }
      catch (Exception)
      {
        entityPart = null;
        return false;
      }
      return true;
    }

    /// <summary>
    /// Gets the entity part.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <returns></returns>
    public EntityPart<T> GetEntityPart<T>(string containerTitle, Guid entityId, string partName)
    {
      EntityPart untypedEntityPart = GetEntityPart(containerTitle, entityId, partName);

      if (untypedEntityPart == null)
        return null;

      return new EntityPart<T>(untypedEntityPart);
    }

    /// <summary>
    /// Renames the entity part.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <param name="newPartName">New name of the part.</param>
    /// <returns></returns>
    public virtual bool RenameEntityPart(string containerTitle, Guid entityId, string partName, string newPartName)
    {
      if (newPartName + Constants.DocumentSetEntityPartExtension == Constants.DocumentStoreDefaultEntityPartFileName)
        throw new InvalidOperationException("Filename is reserved.");

      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, entityId, partName, out entityPart) == false)
            return false;

          web.AllowUnsafeUpdates = true;
          try
          {
            entityPart.Item["Name"] = newPartName + Constants.DocumentSetEntityPartExtension;
            entityPart.Item.UpdateOverwriteVersion();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return true;
        }
      }
    }

    /// <summary>
    /// Updates the entity part.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public bool UpdateEntityPart<T>(string containerTitle, Guid entityId, string partName, T value)
    {
      var entityPart = GetEntityPart<T>(containerTitle, entityId, partName);
      entityPart.Data = DocumentStoreHelper.SerializeObjectToJson<T>(value);
      return UpdateEntityPart(containerTitle, entityId, entityPart);
    }

    /// <summary>
    /// Updates the entity part.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="entityPart">The entity part.</param>
    /// <returns></returns>
    public virtual bool UpdateEntityPart(string containerTitle, Guid entityId, EntityPart entityPart)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          SPFile entityPartFile;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, entityId, entityPart.Name, out entityPartFile) == false)
            return false;

          var entityPartContentType = list.ContentTypes.OfType<SPContentType>().Where(ct => ct.Id.ToString().ToLowerInvariant().StartsWith(Constants.DocumentStoreEntityPartContentTypeId.ToLowerInvariant())).FirstOrDefault();

          Hashtable properties = new Hashtable();
          properties.Add("ContentTypeId", entityPartContentType.Id.ToString());
          properties.Add("Content Type", entityPartContentType.Name);
          properties.Add("Category", entityPart.Category);

          web.AllowUnsafeUpdates = true;
          try
          {
            if (entityPartFile.ParentFolder.Item.DoesUserHavePermissions(SPBasePermissions.EditListItems) == false)
              throw new InvalidOperationException("Insufficent Permissions.");

            entityPartFile = entityPartFile.ParentFolder.Files.Add(entityPartFile.Name, System.Text.UTF8Encoding.Default.GetBytes(entityPart.Data), properties, true);
            return false;
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }
        }
      }
    }

    /// <summary>
    /// Deletes the entity part.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <returns></returns>
    public virtual bool DeleteEntityPart(string containerTitle, Guid entityId, string partName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, entityId, partName, out entityPart) == false)
            return false;

          web.AllowUnsafeUpdates = true;
          try
          {
            entityPart.Delete();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return true;
        }
      }
    }

    /// <summary>
    /// Lists the entity parts associated with the specified entity in the specified container.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <returns></returns>
    public virtual IList<EntityPart> ListEntityParts(string containerTitle, Guid entityId)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          var entityPartContentType = list.ContentTypes.OfType<SPContentType>().Where(ct => ct.Id.ToString().ToLowerInvariant().StartsWith(Constants.DocumentStoreEntityPartContentTypeId.ToLowerInvariant())).FirstOrDefault();

          List<EntityPart> result = new List<EntityPart>();
          foreach (SPFile entityPartFile in defaultEntityPart.ParentFolder.Files.OfType<SPFile>().Where(f => f.Item.ContentTypeId == entityPartContentType.Id && f.Name != Constants.DocumentStoreDefaultEntityPartFileName))
          {
            result.Add(SPDocumentStoreHelper.MapEntityPartFromSPFile(entityPartFile));
          }

          return result;
        }
      }
    }
    #endregion

    #region EntityVersion

    /// <summary>
    /// Lists the entity versions.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <returns></returns>
    public IList<EntityVersion> ListEntityVersions(string containerTitle, Guid entityId)
    {
      List<EntityVersion> result = new List<EntityVersion>();

      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          foreach (var ver in defaultEntityPart.Item.Versions.OfType<SPListItemVersion>())
          {
            var entityVersion = new EntityVersion()
            {
              Comment = ver.ListItem.File.CheckInComment,
              Created = ver.Created,
              CreatedByLoginName = ver.CreatedBy.User.LoginName,
              Entity = SPDocumentStoreHelper.MapEntityFromSPListItemVersion(ver),
              IsCurrentVersion = ver.IsCurrentVersion,
              VersionId = ver.VersionId,
              VersionLabel = ver.VersionLabel,
            };

            ProcessEntityFile(containerTitle, entityId, ver.ListItem.File, entityVersion.Entity);

            result.Add(entityVersion);
          }
        }
      }

      return result;
    }

    /// <summary>
    /// Gets the entity version.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="versionId">The version id.</param>
    /// <returns></returns>
    public EntityVersion GetEntityVersion(string containerTitle, Guid entityId, int versionId)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          var ver = defaultEntityPart.Item.Versions.OfType<SPListItemVersion>().Where(v => v.VersionId == versionId).FirstOrDefault();

          if (ver == null)
            return null;

          var entityVersion = new EntityVersion()
          {
            Comment = ver.ListItem.File.CheckInComment,
            Created = ver.Created,
            CreatedByLoginName = ver.CreatedBy.User.LoginName,
            Entity = SPDocumentStoreHelper.MapEntityFromSPListItemVersion(ver),
            IsCurrentVersion = ver.IsCurrentVersion,
            VersionId = ver.VersionId,
            VersionLabel = ver.VersionLabel,
          };

          ProcessEntityFile(containerTitle, entityId, ver.ListItem.File, entityVersion.Entity);

          return entityVersion;
        }
      }
    }

    /// <summary>
    /// Reverts the entity to version.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="versionId">The version id.</param>
    /// <returns></returns>
    public EntityVersion RevertEntityToVersion(string containerTitle, Guid entityId, int versionId)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          var entityVersion = GetEntityVersion(containerTitle, entityId, versionId);

          UpdateEntity(containerTitle, entityVersion.Entity);

          var versions = ListEntityVersions(containerTitle, entityId);

          return versions.Where(v => v.IsCurrentVersion).FirstOrDefault();
        }
      }
    }
    #endregion

    #region Attachments

    /// <summary>
    /// Uploads the attachment and associates it with the specified entity with the specified filename.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="attachment">The attachment.</param>
    /// <returns></returns>
    public virtual Attachment UploadAttachment(string containerTitle, Guid entityId, string fileName, byte[] attachment)
    {
      return UploadAttachment(containerTitle, entityId, fileName, attachment, String.Empty, string.Empty);
    }

    /// <summary>
    /// Uploads the attachment and associates it with the specified entity with the specified filename.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="attachment">The attachment.</param>
    /// <param name="category">The category.</param>
    /// <param name="path">The path.</param>
    /// <returns></returns>
    public virtual Attachment UploadAttachment(string containerTitle, Guid entityId, string fileName, byte[] attachment, string category, string path)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          SPContentType attachmentContentType = list.ContentTypes.OfType<SPContentType>().Where(ct => ct.Id.ToString().ToLowerInvariant().StartsWith(Constants.AttachmentDocumentContentTypeId)).FirstOrDefault();

          Hashtable properties = new Hashtable();
          properties.Add("ContentTypeId", attachmentContentType.Id.ToString());
          properties.Add("Content Type", attachmentContentType.Name);

          if (String.IsNullOrEmpty(category) == false)
            properties.Add("Category", category);

          if (String.IsNullOrEmpty(path) == false)
          {
            Uri pathUri = null;
            if (Uri.TryCreate(path, UriKind.Relative, out pathUri) == false)
            {
              throw new InvalidOperationException("The optional Path parameter is not in the format of a path.");
            }

            properties.Add("AttachmentPath", path);
          }

          web.AllowUnsafeUpdates = true;
          try
          {
            SPFile attachmentFile = defaultEntityPart.ParentFolder.Files.Add(fileName, attachment, properties, true);
            return SPDocumentStoreHelper.MapAttachmentFromSPFile(attachmentFile);
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }
        }
      }
    }

    /// <summary>
    /// Uploads the attachment from the specified source URL and associates it with the specified entity.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="sourceUrl">The source URL.</param>
    /// <param name="category">The category.</param>
    /// <param name="path">The path.</param>
    /// <returns></returns>
    public virtual Attachment UploadAttachmentFromSourceUrl(string containerTitle, Guid entityId, string fileName, string sourceUrl, string category, string path)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          Uri sourceUri = new Uri(sourceUrl);

          byte[] fileContents = null;

          //Get the content via a httpwebrequest and copy it with the same filename.
          HttpWebRequest webRequest;
          HttpWebResponse webResponse = null;

          try
          {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            webRequest = (HttpWebRequest)WebRequest.Create(sourceUri);
            webRequest.Timeout = 10000;
            webRequest.AllowWriteStreamBuffering = false;
            webResponse = (HttpWebResponse)webRequest.GetResponse();

            using (Stream s = webResponse.GetResponseStream())
            {
              byte[] buffer = new byte[32768];
              using (MemoryStream ms = new MemoryStream())
              {
                int read;
                while ((read = s.Read(buffer, 0, buffer.Length)) > 0)
                {
                  ms.Write(buffer, 0, read);
                }
                fileContents = ms.ToArray();
              }
            }
            webResponse.Close();
          }
          catch (Exception)
          {
            if (webResponse != null)
              webResponse.Close();

            throw;
          }

          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          SPContentType attachmentContentType = list.ContentTypes.OfType<SPContentType>().Where(ct => ct.Id.ToString().ToLowerInvariant().StartsWith(Constants.AttachmentDocumentContentTypeId)).FirstOrDefault();

          Hashtable properties = new Hashtable();
          properties.Add("ContentTypeId", attachmentContentType.Id.ToString());
          properties.Add("Content Type", attachmentContentType.Name);

          if (String.IsNullOrEmpty(category) == false)
            properties.Add("Category", category);

          if (String.IsNullOrEmpty(path) == false)
            properties.Add("Path", path);

          web.AllowUnsafeUpdates = true;
          try
          {
            SPFile attachmentFile = defaultEntityPart.ParentFolder.Files.Add(fileName, fileContents, properties, true);
            return SPDocumentStoreHelper.MapAttachmentFromSPFile(attachmentFile);
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }
        }
      }
    }

    /// <summary>
    /// Gets an attachment associated with the specified entity with the specified filename.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <returns></returns>
    public virtual Attachment GetAttachment(string containerTitle, Guid entityId, string fileName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return null;

          return SPDocumentStoreHelper.MapAttachmentFromSPFile(attachment);
        }
      }
    }

    /// <summary>
    /// Renames the attachment with the specified filename to the new filename.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="newFileName">New name of the file.</param>
    /// <returns></returns>
    public virtual bool RenameAttachment(string containerTitle, Guid entityId, string fileName, string newFileName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return false;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return false;

          web.AllowUnsafeUpdates = true;
          try
          {
            attachment.Item["Name"] = newFileName;
            attachment.Item.Update();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return true;
        }
      }
    }

    /// <summary>
    /// Deletes the attachment from the document store.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <returns></returns>
    public virtual bool DeleteAttachment(string containerTitle, Guid entityId, string fileName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return false;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return false;

          web.AllowUnsafeUpdates = true;
          try
          {
            attachment.Delete();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return true;
        }
      }
    }

    /// <summary>
    /// Lists the attachments associated with the specified entity.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <returns></returns>
    public virtual IList<Attachment> ListAttachments(string containerTitle, Guid entityId)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          var attachmentContentType = list.ContentTypes.OfType<SPContentType>().Where(ct => ct.Id.ToString().ToLowerInvariant().StartsWith(Constants.AttachmentDocumentContentTypeId.ToLowerInvariant())).FirstOrDefault();

          List<Attachment> result = new List<Attachment>();
          foreach (SPFile attachmentFile in defaultEntityPart.ParentFolder.Files.OfType<SPFile>().Where(f => f.Item.ContentTypeId == attachmentContentType.Id))
          {
            result.Add(SPDocumentStoreHelper.MapAttachmentFromSPFile(attachmentFile));
          }

          return result;
        }
      }
    }

    /// <summary>
    /// Downloads the attachment as a stream.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <returns></returns>
    public virtual Stream DownloadAttachment(string containerTitle, Guid entityId, string fileName)
    {
      MemoryStream result = new MemoryStream();

      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return null;

          var attachmentStream = attachment.OpenBinaryStream();
          DocumentStoreHelper.CopyStream(attachmentStream, result);
        }
      }
      result.Seek(0, SeekOrigin.Begin);
      return result;
    }
    #endregion

    #region Metadata

    /// <summary>
    /// Gets the container metadata.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="key">The key.</param>
    /// <returns></returns>
    public virtual string GetContainerMetadata(string containerTitle, string key)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          if (list.RootFolder.Properties.ContainsKey(Constants.MetadataPrefix + key))
            return list.RootFolder.Properties[Constants.MetadataPrefix + key] as string;
          return null;
        }
      }
    }

    /// <summary>
    /// Sets the container metadata.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public virtual bool SetContainerMetadata(string containerTitle, string key, string value)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          web.AllowUnsafeUpdates = true;
          try
          {
            list.RootFolder.Properties[Constants.MetadataPrefix + key] = value;
            list.RootFolder.Update();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return true;
        }
      }
    }

    /// <summary>
    /// Lists the container metadata.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <returns></returns>
    public virtual IDictionary<string, string> ListContainerMetadata(string containerTitle)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          Dictionary<string, string> result = new Dictionary<string, string>();

          foreach (string key in list.RootFolder.Properties.Keys.OfType<string>().Where(k => k.StartsWith(Constants.MetadataPrefix)))
          {
            result.Add(key.Substring(Constants.MetadataPrefix.Length), list.RootFolder.Properties[key] as string);
          }
          return result;
        }
      }
    }

    /// <summary>
    /// Gets the entity metadata.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="key">The key.</param>
    /// <returns></returns>
    public virtual string GetEntityMetadata(string containerTitle, Guid entityId, string key)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          if (defaultEntityPart.Properties.ContainsKey(Constants.MetadataPrefix + key))
            return defaultEntityPart.Properties[Constants.MetadataPrefix + key] as string;
          return null;
        }
      }
    }

    /// <summary>
    /// Sets the entity metadata.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public virtual bool SetEntityMetadata(string containerTitle, Guid entityId, string key, string value)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return false;

          web.AllowUnsafeUpdates = true;
          try
          {
            defaultEntityPart.Properties[Constants.MetadataPrefix + key] = value;
            defaultEntityPart.Update();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return true;
        }
      }
    }

    /// <summary>
    /// Lists the entity metadata.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <returns></returns>
    public virtual IDictionary<string, string> ListEntityMetadata(string containerTitle, Guid entityId)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          Dictionary<string, string> result = new Dictionary<string, string>();

          foreach (string key in defaultEntityPart.Properties.Keys.OfType<string>().Where(k => k.StartsWith(Constants.MetadataPrefix)))
          {
            result.Add(key.Substring(Constants.MetadataPrefix.Length), defaultEntityPart.Properties[key] as string);
          }
          return result;
        }
      }
    }

    /// <summary>
    /// Gets the entity part metadata.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <param name="key">The key.</param>
    /// <returns></returns>
    public virtual string GetEntityPartMetadata(string containerTitle, Guid entityId, string partName, string key)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, entityId, partName, out entityPart) == false)
            return null;

          if (entityPart.Properties.ContainsKey(Constants.MetadataPrefix + key))
            return entityPart.Properties[Constants.MetadataPrefix + key] as string;
          return null;
        }
      }
    }

    /// <summary>
    /// Sets the entity part metadata.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public virtual bool SetEntityPartMetadata(string containerTitle, Guid entityId, string partName, string key, string value)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, entityId, partName, out entityPart) == false)
            return false;

          web.AllowUnsafeUpdates = true;

          try
          {
            entityPart.Properties[Constants.MetadataPrefix + key] = value;
            entityPart.Update();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return true;
        }
      }
    }

    /// <summary>
    /// Lists the entity part metadata.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <returns></returns>
    public virtual IDictionary<string, string> ListEntityPartMetadata(string containerTitle, Guid entityId, string partName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, entityId, partName, out entityPart) == false)
            return null;

          Dictionary<string, string> result = new Dictionary<string, string>();

          foreach (string key in entityPart.Properties.Keys.OfType<string>().Where(k => k.StartsWith(Constants.MetadataPrefix)))
          {
            result.Add(key.Substring(Constants.MetadataPrefix.Length), entityPart.Properties[key] as string);
          }
          return result;
        }
      }
    }

    /// <summary>
    /// Gets the attachment metadata.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="key">The key.</param>
    /// <returns></returns>
    public virtual string GetAttachmentMetadata(string containerTitle, Guid entityId, string fileName, string key)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return null;

          if (attachment.Properties.ContainsKey(Constants.MetadataPrefix + key))
            return attachment.Properties[Constants.MetadataPrefix + key] as string;
          return null;
        }
      }
    }

    /// <summary>
    /// Sets the attachment metadata.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public virtual bool SetAttachmentMetadata(string containerTitle, Guid entityId, string fileName, string key, string value)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return false;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return false;

          web.AllowUnsafeUpdates = true;
          try
          {
            attachment.Properties[Constants.MetadataPrefix + key] = value;
            attachment.Update();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return true;
        }
      }
    }

    /// <summary>
    /// Lists the attachment metadata.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <returns></returns>
    public virtual IDictionary<string, string> ListAttachmentMetadata(string containerTitle, Guid entityId, string fileName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return null;

          Dictionary<string, string> result = new Dictionary<string, string>();

          foreach (string key in attachment.Properties.Keys.OfType<string>().Where(k => k.StartsWith(Constants.MetadataPrefix)))
          {
            result.Add(key.Substring(Constants.MetadataPrefix.Length), attachment.Properties[key] as string);
          }
          return result;
        }
      }
    }
    #endregion

    #region Comments
    /// <summary>
    /// Adds a comment to the specified entity.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="comment">The comment.</param>
    /// <returns></returns>
    public virtual Comment AddEntityComment(string containerTitle, Guid entityId, string comment)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          web.AllowUnsafeUpdates = true;
          try
          {
            defaultEntityPart.Item[Constants.CommentFieldId] = comment;
            defaultEntityPart.Item.Update();

            return SPDocumentStoreHelper.MapCommentFromSPListItem(defaultEntityPart.Item);
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }
        }
      }
    }

    /// <summary>
    /// Lists the comments associated with the specified entity.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <returns></returns>
    public virtual IList<Comment> ListEntityComments(string containerTitle, Guid entityId)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          List<Comment> result = new List<Comment>();
          Comment lastComment = new Comment()
          {
            CommentText = null
          };

          foreach (var itemVersion in defaultEntityPart.Item.Versions.OfType<SPListItemVersion>().OrderBy(v => Double.Parse(v.VersionLabel)))
          {
            if (String.Compare(itemVersion["Comments"] as string, lastComment.CommentText) != 0)
            {
              lastComment = SPDocumentStoreHelper.MapCommentFromSPListItemVersion(itemVersion);
              result.Add(lastComment);
            }
          }
          result.Reverse();
          return result;
        }
      }
    }

    /// <summary>
    /// Adds a comment to the specified entity part.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <param name="comment">The comment.</param>
    /// <returns></returns>
    public virtual Comment AddEntityPartComment(string containerTitle, Guid entityId, string partName, string comment)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, entityId, partName, out entityPart) == false)
            return null;

          web.AllowUnsafeUpdates = true;
          try
          {
            entityPart.Item[Constants.CommentFieldId] = comment;
            entityPart.Item.Update();

            return SPDocumentStoreHelper.MapCommentFromSPListItem(entityPart.Item);
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }
        }
      }
    }

    /// <summary>
    /// Lists the comments associated with the specified entity part.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="partName">Name of the part.</param>
    /// <returns></returns>
    public virtual IList<Comment> ListEntityPartComments(string containerTitle, Guid entityId, string partName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, entityId, partName, out entityPart) == false)
            return null;

          List<Comment> result = new List<Comment>();
          Comment lastComment = new Comment()
          {
            CommentText = null
          };

          foreach (var itemVersion in entityPart.Item.Versions.OfType<SPListItemVersion>().OrderBy(v => Double.Parse(v.VersionLabel)))
          {
            if (String.Compare(itemVersion["Comments"] as string, lastComment.CommentText) != 0)
            {
              lastComment = SPDocumentStoreHelper.MapCommentFromSPListItemVersion(itemVersion);
              result.Add(lastComment);
            }
          }
          result.Reverse();
          return result;
        }
      }
    }

    /// <summary>
    /// Adds a comment to the specified attachment.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="comment">The comment.</param>
    /// <returns></returns>
    public virtual Comment AddAttachmentComment(string containerTitle, Guid entityId, string fileName, string comment)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return null;

          web.AllowUnsafeUpdates = true;
          try
          {
            attachment.Item[Constants.CommentFieldId] = comment;
            attachment.Item.Update();

            return SPDocumentStoreHelper.MapCommentFromSPListItem(attachment.Item);
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }
        }
      }
    }

    /// <summary>
    /// Lists the comments associated with the specified attachment.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <returns></returns>
    public virtual IList<Comment> ListAttachmentComments(string containerTitle, Guid entityId, string fileName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return null;

          List<Comment> result = new List<Comment>();
          Comment lastComment = new Comment()
          {
            CommentText = null
          };

          foreach (var itemVersion in attachment.Item.Versions.OfType<SPListItemVersion>().OrderBy(v => Double.Parse(v.VersionLabel)))
          {
            if (String.Compare(itemVersion["Comments"] as string, lastComment.CommentText) != 0)
            {
              lastComment = SPDocumentStoreHelper.MapCommentFromSPListItemVersion(itemVersion);
              result.Add(lastComment);
            }
          }
          result.Reverse();
          return result;
        }
      }
    }
    #endregion

    #region Container Permissions
    /// <summary>
    /// Adds the principal role to container.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="principalName">Name of the principal.</param>
    /// <param name="principalType">Type of the principal.</param>
    /// <param name="roleName">Name of the role.</param>
    /// <returns></returns>
    public virtual PrincipalRoleInfo AddPrincipalRoleToContainer(string containerTitle, string principalName, string principalType, string roleName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {

          SPList list = web.Lists.TryGetList(containerTitle);
          if ((list == null) || (list.TemplateFeatureId != Constants.DocumentContainerFeatureId))
            throw new InvalidOperationException("A Container with the specified title does not exist.");

          SPPrincipal currentPrincipal = PermissionsHelper.GetPrincipal(web, principalName, principalType);

          if (currentPrincipal == null)
            return null;

          SPRoleType roleType = PermissionsHelper.GetRoleType(web, roleName);

          PermissionsHelper.AddListPermissionsForPrincipal(web, list, currentPrincipal, roleType);

          return PermissionsHelper.MapPrincipalRoleInfoFromSPSecurableObject(list, currentPrincipal);
        }
      }
    }

    /// <summary>
    /// Removes the principal role from container.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="principalName">Name of the principal.</param>
    /// <param name="principalType">Type of the principal.</param>
    /// <param name="roleName">Name of the role.</param>
    /// <returns></returns>
    public virtual bool RemovePrincipalRoleFromContainer(string containerTitle, string principalName, string principalType, string roleName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list = web.Lists.TryGetList(containerTitle);
          if ((list == null) || (list.TemplateFeatureId != Constants.DocumentContainerFeatureId))
            throw new InvalidOperationException("A Container with the specified title does not exist.");

          list.CheckPermissions(SPBasePermissions.ManagePermissions);
          SPPrincipal currentPrincipal = PermissionsHelper.GetPrincipal(web, principalName, principalType);

          if (currentPrincipal == null)
            return false;

          SPRoleType roleType = PermissionsHelper.GetRoleType(web, roleName);

          return PermissionsHelper.RemoveListPermissionsForPrincipal(web, list, currentPrincipal, roleType);
        }
      }
    }

    /// <summary>
    /// Gets the container permissions.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <returns></returns>
    public virtual PermissionsInfo GetContainerPermissions(string containerTitle)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {

          SPList list = web.Lists.TryGetList(containerTitle);
          if ((list == null) || (list.TemplateFeatureId != Constants.DocumentContainerFeatureId))
            throw new InvalidOperationException("A Container with the specified title does not exist.");

          return PermissionsHelper.MapPermissionsFromSPSecurableObject(list);
        }
      }
    }

    /// <summary>
    /// Gets the container permissions for principal.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="principalName">Name of the principal.</param>
    /// <param name="principalType">Type of the principal.</param>
    /// <returns></returns>
    public virtual PrincipalRoleInfo GetContainerPermissionsForPrincipal(string containerTitle, string principalName, string principalType)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {

          SPList list = web.Lists.TryGetList(containerTitle);
          if ((list == null) || (list.TemplateFeatureId != Constants.DocumentContainerFeatureId))
            throw new InvalidOperationException("A Container with the specified title does not exist.");

          var currentPrincipal = PermissionsHelper.GetPrincipal(web, principalName, principalType);

          if (currentPrincipal == null)
            return null;

          return PermissionsHelper.MapPrincipalRoleInfoFromSPSecurableObject(list, currentPrincipal);
        }
      }
    }

    /// <summary>
    /// Resets the container permissions.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <returns></returns>
    public virtual PermissionsInfo ResetContainerPermissions(string containerTitle)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {

          SPList list = web.Lists.TryGetList(containerTitle);
          if ((list == null) || (list.TemplateFeatureId != Constants.DocumentContainerFeatureId))
            throw new InvalidOperationException("A Container with the specified title does not exist.");

          web.AllowUnsafeUpdates = true;

          try
          {
            list.ResetRoleInheritance();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return PermissionsHelper.MapPermissionsFromSPSecurableObject(list);
        }
      }
    }
    #endregion

    #region Entity Permissions

    /// <summary>
    /// Adds the principal role to entity.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <param name="principalName">Name of the principal.</param>
    /// <param name="principalType">Type of the principal.</param>
    /// <param name="roleName">Name of the role.</param>
    /// <returns></returns>
    public virtual PrincipalRoleInfo AddPrincipalRoleToEntity(string containerTitle, Guid guid, string principalName, string principalType, string roleName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, guid, out defaultEntityPart) == false)
            return null;

          SPPrincipal currentPrincipal = PermissionsHelper.GetPrincipal(web, principalName, principalType);

          if (currentPrincipal == null)
            return null;

          SPRoleType roleType = PermissionsHelper.GetRoleType(web, roleName);

          PermissionsHelper.AddListItemPermissionsForPrincipal(web, defaultEntityPart.ParentFolder.Item, currentPrincipal, roleType, true);

          return PermissionsHelper.MapPrincipalRoleInfoFromSPSecurableObject(defaultEntityPart.ParentFolder.Item, currentPrincipal);
        }
      }
    }

    /// <summary>
    /// Removes the principal role from entity.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <param name="principalName">Name of the principal.</param>
    /// <param name="principalType">Type of the principal.</param>
    /// <param name="roleName">Name of the role.</param>
    /// <returns></returns>
    public virtual bool RemovePrincipalRoleFromEntity(string containerTitle, Guid guid, string principalName, string principalType, string roleName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, guid, out defaultEntityPart) == false)
            return false;

          SPPrincipal currentPrincipal = PermissionsHelper.GetPrincipal(web, principalName, principalType);

          if (currentPrincipal == null)
            return false;

          SPRoleType roleType = PermissionsHelper.GetRoleType(web, roleName);

          return PermissionsHelper.RemoveListItemPermissionsForPrincipal(web, defaultEntityPart.ParentFolder.Item, currentPrincipal, roleType, false);
        }
      }
    }

    /// <summary>
    /// Gets the entity permissions.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <returns></returns>
    public virtual PermissionsInfo GetEntityPermissions(string containerTitle, Guid guid)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, guid, out defaultEntityPart) == false)
            return null;

          return PermissionsHelper.MapPermissionsFromSPSecurableObject(defaultEntityPart.ParentFolder.Item);
        }
      }
    }

    /// <summary>
    /// Gets the entity permissions for principal.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <param name="principalName">Name of the principal.</param>
    /// <param name="principalType">Type of the principal.</param>
    /// <returns></returns>
    public virtual PrincipalRoleInfo GetEntityPermissionsForPrincipal(string containerTitle, Guid guid, string principalName, string principalType)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, guid, out defaultEntityPart) == false)
            return null;

          var currentPrincipal = PermissionsHelper.GetPrincipal(list.ParentWeb, principalName, principalType);

          if (currentPrincipal == null)
            return null;

          return PermissionsHelper.MapPrincipalRoleInfoFromSPSecurableObject(defaultEntityPart.ParentFolder.Item, currentPrincipal);
        }
      }
    }

    /// <summary>
    /// Resets the entity permissions.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <returns></returns>
    public virtual PermissionsInfo ResetEntityPermissions(string containerTitle, Guid guid)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, guid, out defaultEntityPart) == false)
            return null;

          web.AllowUnsafeUpdates = true;

          try
          {
            defaultEntityPart.ParentFolder.Item.ResetRoleInheritance();
            defaultEntityPart.ParentFolder.Item.Update();
            defaultEntityPart.ParentFolder.Update();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return PermissionsHelper.MapPermissionsFromSPSecurableObject(defaultEntityPart.Item);
        }
      }
    }
    #endregion

    #region EntityPart Permissions

    /// <summary>
    /// Adds the principal role to entity part.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <param name="partName">Name of the part.</param>
    /// <param name="principalName">Name of the principal.</param>
    /// <param name="principalType">Type of the principal.</param>
    /// <param name="roleName">Name of the role.</param>
    /// <returns></returns>
    public virtual PrincipalRoleInfo AddPrincipalRoleToEntityPart(string containerTitle, Guid guid, string partName, string principalName, string principalType, string roleName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, guid, partName, out entityPart) == false)
            return null;

          SPPrincipal currentPrincipal = PermissionsHelper.GetPrincipal(web, principalName, principalType);

          if (currentPrincipal == null)
            return null;

          SPRoleType roleType = PermissionsHelper.GetRoleType(web, roleName);

          PermissionsHelper.AddListItemPermissionsForPrincipal(web, entityPart.Item, currentPrincipal, roleType, true);

          return PermissionsHelper.MapPrincipalRoleInfoFromSPSecurableObject(entityPart.Item, currentPrincipal);
        }
      }
    }

    /// <summary>
    /// Removes the principal role from entity part.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <param name="partName">Name of the part.</param>
    /// <param name="principalName">Name of the principal.</param>
    /// <param name="principalType">Type of the principal.</param>
    /// <param name="roleName">Name of the role.</param>
    /// <returns></returns>
    public virtual bool RemovePrincipalRoleFromEntityPart(string containerTitle, Guid guid, string partName, string principalName, string principalType, string roleName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, guid, partName, out entityPart) == false)
            return false;

          SPPrincipal currentPrincipal = PermissionsHelper.GetPrincipal(web, principalName, principalType);

          if (currentPrincipal == null)
            return false;

          SPRoleType roleType = PermissionsHelper.GetRoleType(web, roleName);

          return PermissionsHelper.RemoveListItemPermissionsForPrincipal(web, entityPart.Item, currentPrincipal, roleType, false);
        }
      }
    }

    /// <summary>
    /// Gets the entity part permissions.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <param name="partName">Name of the part.</param>
    /// <returns></returns>
    public virtual PermissionsInfo GetEntityPartPermissions(string containerTitle, Guid guid, string partName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, guid, partName, out entityPart) == false)
            return null;

          return PermissionsHelper.MapPermissionsFromSPSecurableObject(entityPart.Item);
        }
      }
    }

    /// <summary>
    /// Gets the entity part permissions for principal.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <param name="partName">Name of the part.</param>
    /// <param name="principalName">Name of the principal.</param>
    /// <param name="principalType">Type of the principal.</param>
    /// <returns></returns>
    public virtual PrincipalRoleInfo GetEntityPartPermissionsForPrincipal(string containerTitle, Guid guid, string partName, string principalName, string principalType)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, guid, partName, out entityPart) == false)
            return null;

          var currentPrincipal = PermissionsHelper.GetPrincipal(list.ParentWeb, principalName, principalType);

          if (currentPrincipal == null)
            return null;

          return PermissionsHelper.MapPrincipalRoleInfoFromSPSecurableObject(entityPart.Item, currentPrincipal);
        }
      }
    }

    /// <summary>
    /// Resets the entity part permissions.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <param name="partName">Name of the part.</param>
    /// <returns></returns>
    public virtual PermissionsInfo ResetEntityPartPermissions(string containerTitle, Guid guid, string partName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile entityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreEntityPart(list, folder, guid, partName, out entityPart) == false)
            return null;

          web.AllowUnsafeUpdates = true;
          try
          {
            entityPart.Item.ResetRoleInheritance();
            entityPart.Item.Update();
            entityPart.Update();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return PermissionsHelper.MapPermissionsFromSPSecurableObject(entityPart.Item);
        }
      }
    }
    #endregion

    #region Attachment Permissions
    /// <summary>
    /// Adds the principal role to attachment.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="principalName">Name of the principal.</param>
    /// <param name="principalType">Type of the principal.</param>
    /// <param name="roleName">Name of the role.</param>
    /// <returns></returns>
    public virtual PrincipalRoleInfo AddPrincipalRoleToAttachment(string containerTitle, Guid guid, string fileName, string principalName, string principalType, string roleName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, guid, out defaultEntityPart) == false)
            return null;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return null;

          SPPrincipal currentPrincipal = PermissionsHelper.GetPrincipal(web, principalName, principalType);

          if (currentPrincipal == null)
            return null;

          SPRoleType roleType = PermissionsHelper.GetRoleType(web, roleName);

          PermissionsHelper.AddListItemPermissionsForPrincipal(web, attachment.Item, currentPrincipal, roleType, true);

          return PermissionsHelper.MapPrincipalRoleInfoFromSPSecurableObject(attachment.Item, currentPrincipal);
        }
      }
    }

    /// <summary>
    /// Removes the principal role from attachment.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="principalName">Name of the principal.</param>
    /// <param name="principalType">Type of the principal.</param>
    /// <param name="roleName">Name of the role.</param>
    /// <returns></returns>
    public virtual bool RemovePrincipalRoleFromAttachment(string containerTitle, Guid guid, string fileName, string principalName, string principalType, string roleName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return false;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, guid, out defaultEntityPart) == false)
            return false;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return false;

          SPPrincipal currentPrincipal = PermissionsHelper.GetPrincipal(web, principalName, principalType);

          if (currentPrincipal == null)
            return false;

          SPRoleType roleType = PermissionsHelper.GetRoleType(web, roleName);

          return PermissionsHelper.RemoveListItemPermissionsForPrincipal(web, attachment.Item, currentPrincipal, roleType, false);
        }
      }
    }

    /// <summary>
    /// Gets the attachment permissions.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <returns></returns>
    public virtual PermissionsInfo GetAttachmentPermissions(string containerTitle, Guid guid, string fileName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, guid, out defaultEntityPart) == false)
            return null;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return null;

          return PermissionsHelper.MapPermissionsFromSPSecurableObject(attachment.Item);
        }
      }
    }

    /// <summary>
    /// Gets the attachment permissions for principal.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="principalName">Name of the principal.</param>
    /// <param name="principalType">Type of the principal.</param>
    /// <returns></returns>
    public virtual PrincipalRoleInfo GetAttachmentPermissionsForPrincipal(string containerTitle, Guid guid, string fileName, string principalName, string principalType)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, guid, out defaultEntityPart) == false)
            return null;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return null;

          var currentPrincipal = PermissionsHelper.GetPrincipal(list.ParentWeb, principalName, principalType);

          if (currentPrincipal == null)
            return null;

          return PermissionsHelper.MapPrincipalRoleInfoFromSPSecurableObject(attachment.Item, currentPrincipal);
        }
      }
    }

    /// <summary>
    /// Resets the attachment permissions.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="guid">The GUID.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <returns></returns>
    public virtual PermissionsInfo ResetAttachmentPermissions(string containerTitle, Guid guid, string fileName)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, String.Empty) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, guid, out defaultEntityPart) == false)
            return null;

          SPFile attachment;
          if (SPDocumentStoreHelper.TryGetDocumentStoreAttachment(list, defaultEntityPart, fileName, out attachment) == false)
            return null;

          web.AllowUnsafeUpdates = true;
          try
          {
            attachment.Item.ResetRoleInheritance();
            attachment.Item.Update();
            attachment.Update();
          }
          finally
          {
            web.AllowUnsafeUpdates = false;
          }

          return PermissionsHelper.MapPermissionsFromSPSecurableObject(attachment.Item);
        }
      }
    }
    #endregion

    #region Locking

    /// <summary>
    /// Gets the entity lock status.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <param name="entityId">The entity id.</param>
    /// <returns></returns>
    public virtual LockStatus GetEntityLockStatus(string containerTitle, string path, Guid entityId)
    {
      var defaultEntityPart = GetDefaultEntityPart(containerTitle, path, entityId);

      if (defaultEntityPart.LockType == SPFile.SPLockType.None || defaultEntityPart.LockId != OrcaDBEntityLockId)
        return LockStatus.Unlocked;

      return LockStatus.Locked;
    }

    /// <summary>
    /// Locks the entity.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <param name="entityId">The entity id.</param>
    public virtual void LockEntity(string containerTitle, string path, Guid entityId, int? timeoutMs)
    {
      bool isLocked = false;

      TimeSpan lockTimeout = TimeSpan.FromSeconds(60);
      if (timeoutMs.HasValue)
        lockTimeout = TimeSpan.FromMilliseconds(timeoutMs.Value);

      do
      {
        var defaultEntityPart = GetDefaultEntityPart(containerTitle, path, entityId);

        if (defaultEntityPart.LockType == SPFile.SPLockType.None)
        {
          defaultEntityPart.Web.AllowUnsafeUpdates = true;
          try
          {
            defaultEntityPart.Lock(SPFile.SPLockType.Exclusive, OrcaDBEntityLockId, lockTimeout);
          }
          catch (SPFileLockException) { /* Do Nothing */ }
          finally
          {
            defaultEntityPart.Web.AllowUnsafeUpdates = false;
          }
        }
        else if (defaultEntityPart.LockType == SPFile.SPLockType.Exclusive &&
                 defaultEntityPart.LockId == OrcaDBEntityLockId)
        {
          //If the entity is locked by the current user, refresh the lock. Otherwise, wait until the lock is released.
          if (defaultEntityPart.LockedByUser != null &&
              defaultEntityPart.LockedByUser.LoginName == this.Web.CurrentUser.LoginName)
          {
            defaultEntityPart.Web.AllowUnsafeUpdates = true;
            try
            {
              defaultEntityPart.RefreshLock(OrcaDBEntityLockId, lockTimeout);
            }
            catch (SPFileLockException) { /* Do Nothing */ }
            finally
            {
              defaultEntityPart.Web.AllowUnsafeUpdates = false;
            }
          }
          else
          {
            WaitForEntityLockRelease(containerTitle, path, entityId, 60000);
            defaultEntityPart.Web.AllowUnsafeUpdates = true;
            try
            {
              defaultEntityPart.Lock(SPFile.SPLockType.Exclusive, OrcaDBEntityLockId, lockTimeout);
            }
            catch (SPFileLockException) { /* Do Nothing */ }
            finally
            {
              defaultEntityPart.Web.AllowUnsafeUpdates = false;
            }
          }
        }

        //Validate that we really got the lock
        defaultEntityPart = GetDefaultEntityPart(containerTitle, path, entityId);
        if (defaultEntityPart.LockType == SPFile.SPLockType.Exclusive &&
            defaultEntityPart.LockId == OrcaDBEntityLockId &&
            defaultEntityPart.LockedByUser != null &&
            defaultEntityPart.LockedByUser.LoginName == this.Web.CurrentUser.LoginName)
        {
          //We're good, we can continue.
          isLocked = true;
        }
      }
      while (isLocked == false);
    }

    /// <summary>
    /// Waits for entity lock release.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <param name="entityId">The entity id.</param>
    /// <param name="timeoutMs">The timeout ms.</param>
    public void WaitForEntityLockRelease(string containerTitle, string path, Guid entityId, int? timeoutMs)
    {
      LockStatus lockStatus = LockStatus.Locked;

      lockStatus = GetEntityLockStatus(containerTitle, path, entityId);

      if (timeoutMs != null)
      {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        while (lockStatus == LockStatus.Locked && sw.ElapsedMilliseconds < timeoutMs)
        {
          System.Threading.Thread.Sleep(250);
          lockStatus = GetEntityLockStatus(containerTitle, path, entityId);
        }

        if (lockStatus == LockStatus.Locked && sw.ElapsedMilliseconds > timeoutMs)
        {
          throw new InvalidOperationException("Timeout occurred while waiting for the lock to release. " + sw.ElapsedMilliseconds);
        }
        sw.Stop();
      }
      else
      {
        while (lockStatus == LockStatus.Locked)
        {
          System.Threading.Thread.Sleep(250);
          lockStatus = GetEntityLockStatus(containerTitle, path, entityId);
        }
      }
    }

    /// <summary>
    /// Unlocks the entity.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <param name="entityId">The entity id.</param>
    public void UnlockEntity(string containerTitle, string path, Guid entityId)
    {
      var defaultEntityPart = GetDefaultEntityPart(containerTitle, path, entityId);

      if (defaultEntityPart.LockType == SPFile.SPLockType.Exclusive &&
              defaultEntityPart.LockId == OrcaDBEntityLockId &&
              defaultEntityPart.LockedByUser != null &&
              defaultEntityPart.LockedByUser.LoginName == this.Web.CurrentUser.LoginName)
      {
        defaultEntityPart.Web.AllowUnsafeUpdates = true;

        try
        {
          defaultEntityPart.ReleaseLock(OrcaDBEntityLockId);
        }
        finally
        {
          defaultEntityPart.Web.AllowUnsafeUpdates = false;
        }
      }
    }

    #endregion

    #region ExecAsync

    public Task ExecAsync(Action action)
    {
      return SPDocumentStoreHelper.ExecuteAsync(HttpContext.Current, this.Web, action);
    }

    #endregion

    #region Private Methods
    /// <summary>
    /// Gets the default entity part SPFile contianed in the specified container title and path with the specified Id.
    /// </summary>
    /// <param name="containerTitle">The container title.</param>
    /// <param name="path">The path.</param>
    /// <param name="entityId">The entity id.</param>
    /// <returns></returns>
    private SPFile GetDefaultEntityPart(string containerTitle, string path, Guid entityId)
    {
      //Get a new web in case we're executing in elevated permissions.
      using (SPSite site = new SPSite(this.Web.Site.ID))
      {
        using (SPWeb web = site.OpenWeb(this.Web.ID))
        {
          SPList list;
          SPFolder folder;
          if (SPDocumentStoreHelper.TryGetFolderFromPath(web, containerTitle, out list, out folder, path) == false)
            return null;

          SPFile defaultEntityPart;
          if (SPDocumentStoreHelper.TryGetDocumentStoreDefaultEntityPart(list, folder, entityId, out defaultEntityPart) == false)
            return null;

          return defaultEntityPart;
        }
      }
    }
    #endregion

    #region IDisposable
    private bool m_disposed;
    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
      Dispose(true);

      // Use SupressFinalize in case a subclass
      // of this type implements a finalizer.
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
      // If you need thread safety, use a lock around these 
      // operations, as well as in your methods that use the resource.
      if (!m_disposed)
      {
        if (disposing)
        {
          SPSecurity.CatchAccessDeniedException = m_originalCatchAccessDeniedValue;
        }
        m_disposed = true;
      }
    }
    #endregion
  }
}
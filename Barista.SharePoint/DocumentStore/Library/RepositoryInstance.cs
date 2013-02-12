﻿namespace Barista.SharePoint.DocumentStore.Library
{
  using Barista.DocumentStore;
  using Barista.Library;
  using Jurassic;
  using Jurassic.Library;
  using System;

  [Serializable]
  public class RepositoryInstance : ObjectInstance
  {
    readonly Repository m_repository;

    public RepositoryInstance(ScriptEngine engine, Repository repository)
      : base(engine)
    {
      if (repository == null)
        throw new ArgumentNullException("repository");

      m_repository = repository;

      this.PopulateFields();
      this.PopulateFunctions();
    }

    #region Properties
    [JSProperty(Name = "containerTitle")]
    public string ContainerTitle
    {
      get { return m_repository.Configuration.ContainerTitle; }
      set { m_repository.Configuration.ContainerTitle = value; }
    }
    #endregion

    #region Container
    [JSFunction(Name = "listContainers")]
    public ArrayInstance ListContainers()
    {
      var result = this.Engine.Array.Construct();
      foreach (var container in m_repository.ListContainers())
      {
        ArrayInstance.Push(result, new ContainerInstance(this.Engine, container));
      }

      return result;
    }

    [JSFunction(Name = "createContainer")]
    public ContainerInstance CreateContainer(string containerTitle, string description)
    {
      var container = m_repository.CreateContainer(containerTitle, description);
      return new ContainerInstance(this.Engine, container);
    }

    [JSFunction(Name = "deleteContainer")]
    public void DeleteContainer(string containerTitle)
    {
      m_repository.DeleteContainer(containerTitle);
    }
    #endregion

    #region Folder
    [JSFunction(Name="listFolders")]
    public ArrayInstance ListFolders(object path)
    {
      string stringPath;
      if (path is FolderInstance)
        stringPath = (path as FolderInstance).FullPath;
      else if (path == Undefined.Value || path == Null.Value || path == null)
        stringPath = String.Empty;
      else
        stringPath = path.ToString();

      var result = this.Engine.Array.Construct();

      var folders = m_repository.ListFolders(stringPath);

      if (folders != null)
      {
        foreach (var folder in folders)
        {
          ArrayInstance.Push(result, new FolderInstance(this.Engine, folder));
        }
      }

      return result;
    }

    [JSFunction(Name="createFolder")]
    public FolderInstance CreateFolder(object path)
    {
      string stringPath;
      if (path is FolderInstance)
        stringPath = (path as FolderInstance).FullPath;
      else if (path == Undefined.Value || path == Null.Value || path == null)
        stringPath = String.Empty;
      else
        stringPath = path.ToString();

      if (String.IsNullOrEmpty(stringPath))
        throw new JavaScriptException(this.Engine, "Error", "The path to create must be specified.");

      var folder = m_repository.CreateFolder(stringPath);
      return new FolderInstance(this.Engine, folder);
    }

    [JSFunction(Name="deleteFolder")]
    public void DeleteFolder(object path)
    {
      string stringPath;
      if (path is FolderInstance)
        stringPath = (path as FolderInstance).FullPath;
      else if (path == Undefined.Value || path == Null.Value || path == null)
        stringPath = String.Empty;
      else
        stringPath = path.ToString();

      if (String.IsNullOrEmpty(stringPath))
        throw new JavaScriptException(this.Engine, "Error", "The path to delete must be specified.");

      m_repository.DeleteFolder(stringPath);
    }

    #endregion

    #region Entity
    [JSFunction(Name = "createEntity")]
    public EntityInstance CreateEntity(object path, object title, object entityNamespace, object data)
    {
      string stringPath;
      var stringTitle = "";
      var stringEntityNamespace = "";
      string stringData;

      if (path is FolderInstance)
        stringPath = (path as FolderInstance).FullPath;
      else if (path == Undefined.Value || path == Null.Value || path == null)
        stringPath = String.Empty;
      else
        stringPath = path.ToString();

      if (title != Null.Value && title != Undefined.Value)
        stringTitle = title.ToString();

      if (entityNamespace != Null.Value && entityNamespace != Undefined.Value)
        stringEntityNamespace = entityNamespace.ToString();

      if (data is ObjectInstance)
// ReSharper disable RedundantArgumentDefaultValue
        stringData = JSONObject.Stringify(this.Engine, data, null, null);
// ReSharper restore RedundantArgumentDefaultValue
      else
        stringData = data.ToString();

      var result = m_repository.CreateEntity(stringPath, stringTitle, stringEntityNamespace, stringData);
      return new EntityInstance(this.Engine, result);
    }

    [JSFunction(Name = "cloneEntity")]
    public EntityInstance CloneEntity(object entityId, string sourcePath, string targetPath, string newTitle)
    {
      if (entityId == Null.Value || entityId == Undefined.Value || entityId == null)
        throw new JavaScriptException(this.Engine, "Error", "Entity Id must be specified.");

      var id = ConvertFromJsObjectToGuid(entityId);

      var result = m_repository.CloneEntity(id, sourcePath, targetPath, newTitle);
      return new EntityInstance(this.Engine, result);
    }

    [JSFunction(Name = "deleteEntity")]
    public bool DeleteEntity(object entityId)
    {
      if (entityId == Null.Value || entityId == Undefined.Value || entityId == null)
        throw new JavaScriptException(this.Engine, "Error", "Entity Id must be specified.");

      var id = ConvertFromJsObjectToGuid(entityId);

      return m_repository.DeleteEntity(id);
    }

    [JSFunction(Name = "getEntity")]
    public object GetEntity(object entityId, object path)
    {
      if (entityId == Null.Value || entityId == Undefined.Value || entityId == null)
        throw new JavaScriptException(this.Engine, "Error", "Entity Id must be specified.");

      var id = ConvertFromJsObjectToGuid(entityId);

      string stringPath;
      if (path is FolderInstance)
        stringPath = (path as FolderInstance).FullPath;
      else if (path == Undefined.Value || path == Null.Value || path == null)
        stringPath = String.Empty;
      else
        stringPath = path.ToString();

      var result = m_repository.GetEntity(id, stringPath);

      if (result == null)
        return Null.Value;

      return new EntityInstance(this.Engine, result);
    }

    [JSFunction(Name = "single")]
    public object Single(object filterCriteria)
    {
      var criteria = new EntityFilterCriteriaInstance(this.Engine.Object.InstancePrototype);

      if (filterCriteria is FolderInstance)
        criteria.EntityFilterCriteria.Path = (filterCriteria as FolderInstance).FullPath;
      else if (filterCriteria is string || filterCriteria is StringInstance || filterCriteria is ConcatenatedString)
        criteria.EntityFilterCriteria.Path = filterCriteria.ToString();
      else if (filterCriteria != null && filterCriteria != Null.Value && filterCriteria != Undefined.Value)
        criteria = JurassicHelper.Coerce<EntityFilterCriteriaInstance>(this.Engine, filterCriteria);

      var result = m_repository.Single(criteria.EntityFilterCriteria);

      if (result == null)
        return Null.Value;

      return new EntityInstance(this.Engine, result);
    }

    [JSFunction(Name = "listEntities")]
    public ArrayInstance ListEntities(object filterCriteria)
    {
      var criteria = new EntityFilterCriteriaInstance(this.Engine.Object.InstancePrototype);

      if (filterCriteria is FolderInstance)
        criteria.EntityFilterCriteria.Path = (filterCriteria as FolderInstance).FullPath;
      else if (filterCriteria is string || filterCriteria is StringInstance || filterCriteria is ConcatenatedString)
        criteria.EntityFilterCriteria.Path = filterCriteria.ToString();
      else if (filterCriteria != null && filterCriteria != Null.Value && filterCriteria != Undefined.Value)
        criteria = JurassicHelper.Coerce<EntityFilterCriteriaInstance>(this.Engine, filterCriteria);

      var result = this.Engine.Array.Construct();

      foreach(var entity in m_repository.ListEntities(criteria.EntityFilterCriteria))
      {
        ArrayInstance.Push(result, new EntityInstance(this.Engine, entity));
      }

      return result;
    }

    [JSFunction(Name = "countEntities")]
    public int CountEntities(object filterCriteria)
    {
      var criteria = new EntityFilterCriteriaInstance(this.Engine.Object.InstancePrototype);

      if (filterCriteria is FolderInstance)
        criteria.EntityFilterCriteria.Path = (filterCriteria as FolderInstance).FullPath;
      else if (filterCriteria is string || filterCriteria is StringInstance || filterCriteria is ConcatenatedString)
        criteria.EntityFilterCriteria.Path = filterCriteria.ToString();
      else if (filterCriteria != null && filterCriteria != Null.Value && filterCriteria != Undefined.Value)
        criteria = JurassicHelper.Coerce<EntityFilterCriteriaInstance>(this.Engine, filterCriteria);

      return m_repository.CountEntities(criteria.EntityFilterCriteria);
    }

    [JSFunction(Name = "moveEntity")]
    public bool MoveEntity(object entityId, string destinationPath)
    {
      if (entityId == Null.Value || entityId == Undefined.Value || entityId == null)
        throw new JavaScriptException(this.Engine, "Error", "Entity Id must be specified.");

      var id = ConvertFromJsObjectToGuid(entityId);

      return m_repository.MoveEntity(id, destinationPath);
    }

    [JSFunction(Name = "updateEntity")]
    public object UpdateEntity(object entityId, object eTag, object data)
    {
      if (entityId == Null.Value || entityId == Undefined.Value || entityId == null)
        throw new JavaScriptException(this.Engine, "Error", "Entity Id must be specified.");

      var id = ConvertFromJsObjectToGuid(entityId);

      string stringData;
      var stringETag = "";

      if (eTag != Null.Value && eTag != Undefined.Value)
        stringETag = eTag.ToString();

      if (data == Null.Value || data == Undefined.Value || data == null)
      {
        if (entityId is EntityInstance)
          stringData = (entityId as EntityInstance).Entity.Data;
        else
          throw new InvalidOperationException("A data parameter must be specified if the first parameter is not an instance of an Entity object.");
      }
      else if (data is ObjectInstance)
// ReSharper disable RedundantArgumentDefaultValue
        stringData = JSONObject.Stringify(this.Engine, data, null, null);
// ReSharper restore RedundantArgumentDefaultValue
      else
        stringData = data.ToString();

      var result = m_repository.UpdateEntityData(id, stringETag, stringData);

      if (result == null)
        return Null.Value;

      return new EntityInstance(this.Engine, result);
    }

    [JSFunction(Name = "updateEntityNamespace")]
    public object UpdateEntityNamespace(object entityId, object newNamespace)
    {
      if (entityId == Null.Value || entityId == Undefined.Value || entityId == null)
        throw new JavaScriptException(this.Engine, "Error", "Entity Id must be specified.");

      var id = ConvertFromJsObjectToGuid(entityId);

      string stringNewNamespace = null;
      if (newNamespace != Null.Value && newNamespace != Undefined.Value)
        stringNewNamespace = newNamespace.ToString();

      var result = m_repository.UpdateEntity(id, null, null, stringNewNamespace);

      if (result == null)
        return Null.Value;

      return new EntityInstance(this.Engine, result);
    }
    #endregion

    #region EntityComments

    [JSFunction(Name = "addEntityComment")]
    public CommentInstance AddEntityComment(object entityId, string comment)
    {
      if (entityId == Null.Value || entityId == Undefined.Value || entityId == null)
        throw new JavaScriptException(this.Engine, "Error", "Entity Id must be specified.");

      var id = ConvertFromJsObjectToGuid(entityId);

      var result = m_repository.AddEntityComment(id, comment);
      return new CommentInstance(this.Engine, result);
    }

    [JSFunction(Name = "listEntityComments")]
    public ArrayInstance ListEntityComments(object entityId)
    {
      if (entityId == Null.Value || entityId == Undefined.Value || entityId == null)
        throw new JavaScriptException(this.Engine, "Error", "Entity Id must be specified.");

      var id = ConvertFromJsObjectToGuid(entityId);

      var result = this.Engine.Array.Construct();

      foreach (var comment in m_repository.ListEntityComments(id))
      {
        ArrayInstance.Push(result, new CommentInstance(this.Engine, comment));
      }

      return result;
    }
    #endregion

    #region Entity Parts
    [JSFunction(Name = "createEntityPart")]
    public EntityPartInstance CreateEntityPart(object entityId, object partName, object category, object data)
    {
      if (entityId == null || entityId == Null.Value || entityId == Undefined.Value)
        throw new JavaScriptException(this.Engine, "Error", "An entity id or an entity must be defined as the first parameter.");

      if (partName == null || partName == Null.Value || partName == Undefined.Value)
        throw new JavaScriptException(this.Engine, "Error", "The name of the desired entity part must be defined as the second parameter.");

      var stringCategory = "";
      var stringData = "";

      string stringPartName = partName.ToString();

      if (String.IsNullOrEmpty(stringPartName))
        throw new JavaScriptException(this.Engine, "Error", "When creating an entity part, a part name must be specified.");

      if (category != Null.Value && category != Undefined.Value || category != null)
        stringCategory = category.ToString();

      if (data is ObjectInstance)
// ReSharper disable RedundantArgumentDefaultValue
        stringData = JSONObject.Stringify(this.Engine, data, null, null);
// ReSharper restore RedundantArgumentDefaultValue
      else if (data != null)
        stringData = data.ToString();

      var id = ConvertFromJsObjectToGuid(entityId);

      return new EntityPartInstance(this.Engine, m_repository.CreateEntityPart(id, stringPartName, stringCategory, stringData));
    }

    [JSFunction(Name = "createOrUpdateEntityPart")]
    public EntityPartInstance CreateOrUpdateEntityPart(object entityId, object partName, object category, object data)
    {
      if (entityId == null || entityId == Null.Value || entityId == Undefined.Value)
        throw new JavaScriptException(this.Engine, "Error", "An entity id or an entity must be defined as the first parameter.");

      var id = ConvertFromJsObjectToGuid(entityId);
      
      var stringPartName = "";
      var stringCategory = "";
      string stringData;

      if (partName != Null.Value && partName != Undefined.Value)
        stringPartName = partName.ToString();

      if (category != Null.Value && category != Undefined.Value)
        stringCategory = category.ToString();

      if (data is ObjectInstance)
// ReSharper disable RedundantArgumentDefaultValue
        stringData = JSONObject.Stringify(this.Engine, data, null, null);
// ReSharper restore RedundantArgumentDefaultValue
      else
        stringData = data.ToString();

      var entityPart = m_repository.GetEntityPart(id, stringPartName);
      entityPart = entityPart == null 
        ? m_repository.CreateEntityPart(id, stringPartName, stringCategory, stringData)
        : m_repository.UpdateEntityPartData(id, stringPartName, stringData);

      return new EntityPartInstance(this.Engine, entityPart);
    }

    [JSFunction(Name = "deleteEntityPart")]
    public bool DeleteEntityPart(object entityId, string partName)
    {
      if (entityId == Null.Value || entityId == Undefined.Value || entityId == null)
        throw new JavaScriptException(this.Engine, "Error", "Either an entity id or an entity must be specified.");

      var id = ConvertFromJsObjectToGuid(entityId);

      return m_repository.DeleteEntityPart(id, partName);
    }

    [JSFunction(Name = "getEntityPart")]
    public object GetEntityPart(object entityId, string partName)
    {
      if (entityId == Null.Value || entityId == Undefined.Value || entityId == null)
        throw new JavaScriptException(this.Engine, "Error", "Either an entity id or an entity must be specified.");

      var id = ConvertFromJsObjectToGuid(entityId);

      var result = m_repository.GetEntityPart(id, partName);

      if (result == null)
        return Null.Value;

      return new EntityPartInstance(this.Engine, result);
    }

    [JSFunction(Name = "hasEntityPart")]
    public bool HasEntityPart(object entityId, string partName)
    {
      if (entityId == Null.Value || entityId == Undefined.Value || entityId == null)
        throw new JavaScriptException(this.Engine, "Error", "Either an entity id or an entity must be specified.");

      var id = ConvertFromJsObjectToGuid(entityId);

      return m_repository.HasEntityPart(id, partName);
    }

    [JSFunction(Name = "listEntityParts")]
    public ObjectInstance ListEntityParts(object entityId)
    {
      var result = this.Engine.Object.Construct();

      if (entityId == Null.Value || entityId == Undefined.Value || entityId == null)
        throw new JavaScriptException(this.Engine, "Error", "Either an entity id or an entity must be specified.");

      Guid id;
      if (entityId is EntityInstance)
        id = (entityId as EntityInstance).Entity.Id;
      else if (entityId is GuidInstance)
        id = (entityId as GuidInstance).Value;
      else
        id = new Guid(entityId.ToString());

      foreach (var entityPart in m_repository.ListEntityParts(id))
      {
        result.SetPropertyValue(entityPart.Name, new EntityPartInstance(this.Engine, entityPart), false);
      }

      return result;
    }

    [JSFunction(Name = "updateEntityPart")]
    public EntityPartInstance UpdateEntityPart(object entityId, object partName, object eTag, object data)
    {
      if (entityId == null || entityId == Null.Value || entityId == Undefined.Value)
        throw new JavaScriptException(this.Engine, "Error", "An entity id or an entity must be defined as the first parameter.");

      var id = ConvertFromJsObjectToGuid(entityId);

      var stringPartName = "";
      string stringData;
      var stringETag = "";

      if (partName != Null.Value && partName != Undefined.Value)
        stringPartName = partName.ToString();

      if (eTag != Null.Value && eTag != Undefined.Value)
        stringETag = eTag.ToString();

      if (data is ObjectInstance)
// ReSharper disable RedundantArgumentDefaultValue
        stringData = JSONObject.Stringify(this.Engine, data, null, null);
// ReSharper restore RedundantArgumentDefaultValue
      else
        stringData = data.ToString();

      var result = m_repository.UpdateEntityPartData(id, stringPartName, stringETag, stringData);
      return new EntityPartInstance(this.Engine, result);
    }
    #endregion

    #region Attachments
    [JSFunction(Name = "listAttachments")]
    public ArrayInstance ListAttachments(object entityId)
    {
      var result = this.Engine.Array.Construct();

      if (entityId == null || entityId == Null.Value || entityId == Undefined.Value)
        throw new JavaScriptException(this.Engine, "Error", "An entity id or an entity must be defined as the first parameter.");

      var id = ConvertFromJsObjectToGuid(entityId);

      foreach (var attachment in m_repository.ListAttachments(id))
      {
        ArrayInstance.Push(result, new AttachmentInstance(this.Engine, attachment));
      }

      return result;
    }

    [JSFunction(Name = "getAttachment")]
    public AttachmentInstance GetAttachment(object entityId, string fileName)
    {
      if (entityId == null || entityId == Null.Value || entityId == Undefined.Value)
        throw new JavaScriptException(this.Engine, "Error", "An entity id or an entity must be defined as the first parameter.");

      var id = ConvertFromJsObjectToGuid(entityId);

      return new AttachmentInstance(this.Engine, m_repository.GetAttachment(id, fileName));
    }

    [JSFunction(Name = "uploadAttachment")]
    public AttachmentInstance UploadAttachment(object entityId, string fileName, Base64EncodedByteArrayInstance attachment)
    {
      if (entityId == null || entityId == Null.Value || entityId == Undefined.Value)
        throw new JavaScriptException(this.Engine, "Error", "An entity id or an entity must be defined as the first parameter.");

      var id = ConvertFromJsObjectToGuid(entityId);

      return new AttachmentInstance(this.Engine, m_repository.UploadAttachment(id, fileName, attachment.Data));
    }

    [JSFunction(Name = "downloadAttachment")]
    public Base64EncodedByteArrayInstance DownloadAttachment(object entityId, string fileName)
    {
      if (entityId == null || entityId == Null.Value || entityId == Undefined.Value)
        throw new JavaScriptException(this.Engine, "Error", "An entity id or an entity must be defined as the first parameter.");

      var id = ConvertFromJsObjectToGuid(entityId);

      var attachment = m_repository.GetAttachment(id, fileName);
      var streamResult = m_repository.DownloadAttachment(id, fileName);

      var result = new Base64EncodedByteArrayInstance(this.Engine.Object.InstancePrototype, streamResult.ReadToEnd())
        {
          MimeType = attachment.MimeType,
          FileName = attachment.FileName
        };

      return result;
    }

    [JSFunction(Name = "deleteAttachment")]
    public bool DeleteAttachment(object entityId, string fileName)
    {
      if (entityId == null || entityId == Null.Value || entityId == Undefined.Value)
        throw new JavaScriptException(this.Engine, "Error", "An entity id or an entity must be defined as the first parameter.");

      var id = ConvertFromJsObjectToGuid(entityId);

      return m_repository.DeleteAttachment(id, fileName);
    }
    #endregion

    #region Permissions
    [JSFunction(Name = "addPrincipalRoleToEntity")]
    public PrincipalRoleInfoInstance AddPrincipalRoleToEntity(string entityId, string principalName, string principalType, string roleName)
    {
      var id = new Guid(entityId);

      var result = m_repository.AddPrincipalRoleToEntity(id, principalName, principalType, roleName);

      return new PrincipalRoleInfoInstance(this.Engine, result);
    }

    [JSFunction(Name = "getEntityPermissions")]
    public PermissionsInfoInstance GetEntityPermissions(string entityId)
    {
      var id = new Guid(entityId);

      var result = m_repository.GetEntityPermissions(id);

      return new PermissionsInfoInstance(this.Engine, result);
    }

    [JSFunction(Name = "removePrincipalRoleFromEntity")]
    public bool RemovePrincipalRoleFromEntity(string entityId, string principalName, string principalType, string roleName)
    {
      var id = new Guid(entityId);

      return m_repository.RemovePrincipalRoleFromEntity(id, principalName, principalType, roleName);
    }

    [JSFunction(Name = "resetEntityPermissions")]
    public PermissionsInfoInstance ResetEntityPermissions(string entityId)
    {
      var id = new Guid(entityId);

      return new PermissionsInfoInstance(this.Engine, m_repository.ResetEntityPermissions(id));
    }
    #endregion

    #region EntitySet
    [JSFunction(Name = "getEntitySet")]
    public EntitySetInstance GetEntitySet(object entityId, object path)
    {
      var stringPath = "";

      if (entityId == null || entityId == Null.Value || entityId == Undefined.Value)
        throw new JavaScriptException(this.Engine, "Error", "An entity id or an entity must be defined as the first parameter.");

      var id = ConvertFromJsObjectToGuid(entityId);

      if (path != Null.Value && path != Undefined.Value)
        stringPath = path.ToString();

      var entity = m_repository.GetEntity(id, stringPath);
      var entityPart = m_repository.ListEntityParts(id);
      return new EntitySetInstance(this.Engine, entity, entityPart);
    }
    #endregion

    #region Helpers
    public static Guid ConvertFromJsObjectToGuid(object guid)
    {
      Guid id;
      if (guid is EntityInstance)
        id = (guid as EntityInstance).Entity.Id;
      else if (guid is GuidInstance)
        id = (guid as GuidInstance).Value;
      else
        id = new Guid(guid.ToString());

      return id;
    }
    #endregion
  }
}
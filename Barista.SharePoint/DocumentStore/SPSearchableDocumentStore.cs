﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Barista.SharePoint.DocumentStore
{
  /// <summary>
  /// Represents a SharePoint-backed document store that is able to be searched via Lucene.Net.
  /// </summary>
  public class SPSearchableDocumentStore : SPMemoryCachedDocumentStore
  {
    public override Barista.DocumentStore.Entity CreateEntity(string containerTitle, string path, string title, string @namespace, string data)
    {
      var entity =  base.CreateEntity(containerTitle, path, @namespace, data);

      ////Insert the data into Lucene.Net
      //try
      //{
      //  var jo = JObject.Parse(data);
        
      //}
      //catch (Exception)
      //{
        
      //}

      return entity;
    }

    public override Barista.DocumentStore.Entity UpdateEntity(string containerTitle, Guid entityId, string title, string description, string @namespace)
    {
      return base.UpdateEntity(containerTitle, entityId, title, description, @namespace);
    }

    public override bool DeleteEntity(string containerTitle, Guid entityId)
    {
      return base.DeleteEntity(containerTitle, entityId);
    }

    public override Barista.DocumentStore.Entity UpdateEntityData(string containerTitle, Guid entityId, string eTag, string data)
    {
      return base.UpdateEntityData(containerTitle, entityId, eTag, data);
    }

    public override Barista.DocumentStore.EntityPart CreateEntityPart(string containerTitle, Guid entityId, string partName, string category, string data)
    {
      return base.CreateEntityPart(containerTitle, entityId, partName, category, data);
    }

    public override Barista.DocumentStore.EntityPart UpdateEntityPart(string containerTitle, Guid entityId, string partName, string category)
    {
      return base.UpdateEntityPart(containerTitle, entityId, partName, category);
    }

    public override Barista.DocumentStore.EntityPart UpdateEntityPartData(string containerTitle, Guid entityId, string partName, string eTag, string data)
    {
      return base.UpdateEntityPartData(containerTitle, entityId, partName, eTag, data);
    }

    public override bool DeleteEntityPart(string containerTitle, Guid entityId, string partName)
    {
      return base.DeleteEntityPart(containerTitle, entityId, partName);
    }
  }
}
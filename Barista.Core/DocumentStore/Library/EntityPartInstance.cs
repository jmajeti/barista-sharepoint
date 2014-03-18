﻿namespace Barista.DocumentStore.Library
{
  using Barista.DocumentStore;
  using Barista.Library;
  using Barista.Newtonsoft.Json;
  using Jurassic;
  using Jurassic.Library;
  using System;

  [Serializable]
  public class EntityPartInstance : ObjectInstance, IEntityPart
  {
    private readonly IEntityPart m_entityPart;

    public EntityPartInstance(ScriptEngine engine, IEntityPart entityPart)
      : base(engine)
    {
      if (entityPart == null)
        throw new ArgumentNullException("entityPart");

      m_entityPart = entityPart;

      this.PopulateFields();
      this.PopulateFunctions();
    }

    public IEntityPart EntityPart
    {
      get { return m_entityPart; }
    }

    [JSProperty(Name = "entityId")]
    public GuidInstance EntityId
    {
      get { return new GuidInstance(this.Engine.Object.InstancePrototype, m_entityPart.EntityId); }
      set
      {
        if (value != null)
          m_entityPart.EntityId = value.Value;
      }
    }

    Guid IEntityPart.EntityId
    {
      get { return m_entityPart.EntityId; }
      set { m_entityPart.EntityId = value; }
    }

    [JSProperty(Name = "name")]
    public string Name
    {
      get { return m_entityPart.Name; }
      set { m_entityPart.Name = value; }
    }

    [JSProperty(Name = "category")]
    public string Category
    {
      get { return m_entityPart.Category; }
      set { m_entityPart.Category = value; }
    }

    [JSProperty(Name = "eTag")]
    public string ETag
    {
      get { return m_entityPart.ETag; }
      set { m_entityPart.ETag = value; }
    }

    [JSProperty(Name = "data")]
    public object Data
    {
      get
      {
        try
        {
          // ReSharper disable RedundantArgumentDefaultValue
          var obj = JsonConvert.DeserializeObject(m_entityPart.Data);
          var value2 = JsonConvert.SerializeObject(obj, Formatting.None);
          var result = JSONObject.Parse(this.Engine, value2, null);
          // ReSharper restore RedundantArgumentDefaultValue
          return result;
        }
        catch (Exception)
        {
          //If there was an issue converting to a JSON object, just return the string value.
          return m_entityPart.Data;
        }
      }
      set
      {

        if (value == Null.Value || value == Undefined.Value || value == null)
          m_entityPart.Data = String.Empty;
        else if (value is string || value is StringInstance || value is ConcatenatedString)
          m_entityPart.Data = value.ToString();
        else if (value is ObjectInstance)
          // ReSharper disable RedundantArgumentDefaultValue
          m_entityPart.Data = JSONObject.Stringify(this.Engine, value, null, null);
        // ReSharper restore RedundantArgumentDefaultValue
        else
          m_entityPart.Data = value.ToString();
      }
    }

    string IEntityPart.Data
    {
      get { return m_entityPart.Data; }
      set { m_entityPart.Data = value; }
    }

    [JSProperty(Name = "created")]
    public DateInstance Created
    {
      get { return JurassicHelper.ToDateInstance(this.Engine, m_entityPart.Created); }
      set { m_entityPart.Created = DateTime.Parse(value.ToIsoString()); }
    }

    DateTime IEntityPart.Created
    {
      get { return m_entityPart.Created; }
      set { m_entityPart.Created = value; }
    }

    [JSProperty(Name = "createdBy")]
    public object CreatedBy
    {
      get
      {
        if (m_entityPart.CreatedBy == null)
          return Null.Value;

        return m_entityPart.CreatedBy.LoginName;
      }
    }

    IUser IEntityPart.CreatedBy
    {
      get { return m_entityPart.CreatedBy; }
      set { m_entityPart.CreatedBy = value; }
    }

    [JSProperty(Name = "modified")]
    public DateInstance Modified
    {
      get { return JurassicHelper.ToDateInstance(this.Engine, m_entityPart.Modified); }
      set { m_entityPart.Modified = DateTime.Parse(value.ToIsoString()); }
    }

    DateTime IEntityPart.Modified
    {
      get { return m_entityPart.Modified; }
      set { m_entityPart.Modified = value; }
    }

    [JSProperty(Name = "modifiedBy")]
    public object ModifiedBy
    {
      get
      {
        if (m_entityPart.ModifiedBy == null)
          return Null.Value;

        return m_entityPart.ModifiedBy.LoginName;
      }
    }

    IUser IEntityPart.ModifiedBy
    {
      get { return m_entityPart.ModifiedBy; }
      set { m_entityPart.ModifiedBy = value; }
    }
  }
}

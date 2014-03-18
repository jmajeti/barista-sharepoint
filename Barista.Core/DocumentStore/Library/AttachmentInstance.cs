﻿namespace Barista.DocumentStore.Library
{
  using Barista.DocumentStore;
  using Jurassic;
  using Jurassic.Library;
  using System;

  [Serializable]
  public class AttachmentInstance : ObjectInstance, IAttachment
  {
    private readonly IAttachment m_attachment;

    public AttachmentInstance(ScriptEngine engine, IAttachment attachment)
      : base(engine)
    {
      if (attachment == null)
        throw new ArgumentNullException("attachment");

      m_attachment = attachment;

      this.PopulateFields();
      this.PopulateFunctions();
    }

    public IAttachment Attachment
    {
      get { return m_attachment; }
    }

    [JSProperty(Name = "fileName")]
    public string FileName
    {
      get { return m_attachment.FileName; }
      set { m_attachment.FileName = value; }
    }

    [JSProperty(Name = "category")]
    public string Category
    {
      get { return m_attachment.Category; }
      set { m_attachment.Category = value; }
    }

    [JSProperty(Name = "path")]
    public string Path
    {
      get { return m_attachment.Path; }
      set { m_attachment.Path = value; }
    }

    [JSProperty(Name = "timeLastModified")]
    public DateInstance TimeLastModified
    {
      get { return JurassicHelper.ToDateInstance(this.Engine, m_attachment.TimeLastModified); }
      set { m_attachment.TimeLastModified = DateTime.Parse(value.ToIsoString()); }
    }

    DateTime IAttachment.TimeLastModified
    {
      get { return m_attachment.TimeLastModified; }
      set { m_attachment.TimeLastModified = value; }
    }

    [JSProperty(Name = "eTag")]
    public string ETag
    {
      get { return m_attachment.ETag; }
      set { m_attachment.ETag = value; }
    }

    [JSProperty(Name = "mimeType")]
    public string MimeType
    {
      get { return m_attachment.MimeType; }
      set { m_attachment.MimeType = value; }
    }

    [JSProperty(Name = "size")]
    public double Size
    {
      get { return m_attachment.Size; }
      set { m_attachment.Size = (long)value; }
    }

    long IAttachment.Size
    {
      get { return m_attachment.Size; }
      set { m_attachment.Size = value; }
    }

    [JSProperty(Name = "url")]
    public string Url
    {
      get { return m_attachment.Url; }
      set { m_attachment.Url = value; }
    }

    [JSProperty(Name = "created")]
    public DateInstance Created
    {
      get { return JurassicHelper.ToDateInstance(this.Engine, m_attachment.Created); }
      set { m_attachment.Created = DateTime.Parse(value.ToIsoString()); }
    }

    DateTime IAttachment.Created
    {
      get { return m_attachment.Created; }
      set { m_attachment.Created = value; }
    }

    [JSProperty(Name = "createdBy")]
    public object CreatedBy
    {
      get
      {
        if (m_attachment.CreatedBy == null)
          return Null.Value;

        return m_attachment.CreatedBy.LoginName;
      }
    }

    IUser IAttachment.CreatedBy
    {
      get { return m_attachment.CreatedBy; }
      set { m_attachment.CreatedBy = value; }
    }

    [JSProperty(Name = "modified")]
    public DateInstance Modified
    {
      get { return JurassicHelper.ToDateInstance(this.Engine, m_attachment.Modified); }
      set { m_attachment.Modified = DateTime.Parse(value.ToIsoString()); }
    }

    DateTime IAttachment.Modified
    {
      get { return m_attachment.Modified; }
      set { m_attachment.Modified = value; }
    }

    [JSProperty(Name = "modifiedBy")]
    public object ModifiedBy
    {
      get
      {
        if (m_attachment.ModifiedBy == null)
          return Null.Value;

        return m_attachment.ModifiedBy.LoginName;
      }
    }

    IUser IAttachment.ModifiedBy
    {
      get { return m_attachment.ModifiedBy; }
      set { m_attachment.ModifiedBy = value; }
    }
  }
}

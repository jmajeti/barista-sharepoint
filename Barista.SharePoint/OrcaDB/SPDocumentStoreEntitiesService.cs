﻿namespace Barista.SharePoint.OrcaDB
{
  using Microsoft.SharePoint.Client.Services;
  using Barista.OrcaDB;
  
  [BasicHttpBindingServiceMetadataExchangeEndpointAttribute]
  public class SPDocumentStoreEntitiesService :
    DocumentStoreEntitiesService, 
    IDocumentStoreEntitiesService
  {
  }
}
﻿namespace Barista
{
  using System.Collections.Generic;
  using System.ServiceModel;
  using Barista.Search;

  [ServiceContract(Namespace = Barista.Constants.ServiceNamespace)]
  public interface IBaristaSearch
  {
    [OperationContract]
    void DeleteDocuments(string indexName, IEnumerable<string> documentIds);

    [OperationContract]
    void DeleteAllDocuments(string indexName);

    [OperationContract]
    Explanation Explain(string indexName, Barista.Search.Query query, int documentId);

    [OperationContract]
    string Highlight(string indexName, Barista.Search.Query query, int documentId, string fieldName, int fragCharSize);

    [OperationContract]
    void IndexDocument(string indexName, string documentId, DocumentDto document);

    [OperationContract]
    void IndexJsonDocument(string indexName, JsonDocumentDto document);

    [OperationContract]
    void IndexJsonDocuments(string indexName, IEnumerable<JsonDocumentDto> documents);

    [OperationContract]
    JsonDocumentDto Retrieve(string indexName, string documentId);

    [OperationContract]
    IList<SearchResult> Search(string indexName, SearchArguments arguments);

    [OperationContract]
    IList<FacetedSearchResult> FacetedSearch(string indexName, SearchArguments arguments);
  }
}
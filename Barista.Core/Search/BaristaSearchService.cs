﻿namespace Barista.Search
{
    using Barista.Extensions;
    using Barista.Logging;
    using Barista.Newtonsoft.Json;
    using Barista.Newtonsoft.Json.Linq;
    using Barista.Search.OData2Lucene;
    using Lucene.Net.Analysis;
    using Lucene.Net.Index;
    using Lucene.Net.Search;
    using Lucene.Net.Search.Vectorhighlight;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.ServiceModel;
    using System.ServiceModel.Web;

    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Single, InstanceContextMode = InstanceContextMode.Single)]
    public class BaristaSearchService : IBaristaSearch, IDisposable
    {
        private const string IndexVersion = "1.0.0.0";
        private static readonly Analyzer DummyAnalyzer = new SimpleAnalyzer();
        private static readonly ConcurrentDictionary<BaristaIndexDefinition, Index> Indexes = new ConcurrentDictionary<BaristaIndexDefinition, Index>();

        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private static readonly ILog StartupLog = LogManager.GetLogger(typeof(BaristaSearchService).FullName + ".Startup");

        /// <summary>
        /// Deletes the documents that have the specified document ids
        /// </summary>
        /// <param name="indexDefinition"></param>
        /// <param name="keys"></param>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest)]
        public void DeleteDocuments(BaristaIndexDefinition indexDefinition, IEnumerable<string> keys)
        {
            try
            {
                var index = GetOrAddIndex(indexDefinition, true);

                try
                {
                    //Remove the documents from the index
                    index.Remove(keys.ToArray());
                }
                catch (OutOfMemoryException)
                {
                    CloseIndexWriter(indexDefinition, false);
                }
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.Message);
            }
        }

        /// <summary>
        /// Deletes all documents from the specified index.
        /// </summary>
        /// <param name="indexDefinition"></param>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest)]
        public void DeleteAllDocuments(BaristaIndexDefinition indexDefinition)
        {
            if (indexDefinition == null)
                throw new ArgumentNullException("indexDefinition");

            try
            {
                var index = GetOrAddIndex(indexDefinition, true);
                try
                {
                    index.DeleteAll();
                }
                catch (OutOfMemoryException)
                {
                    CloseIndexWriter(indexDefinition, false);
                }
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.Message);
            }
        }

        /// <summary>
        /// Returns a value that indicates if an index with the specified name exists.
        /// </summary>
        /// <param name="indexDefinition"></param>
        /// <returns></returns>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest, ResponseFormat = WebMessageFormat.Json)]
        public bool DoesIndexExist(BaristaIndexDefinition indexDefinition)
        {
            var directory = GetLuceneDirectoryFromIndexDefinition(indexDefinition);

            return directory != null;
        }

        /// <summary>
        /// Returns an explanation for a particular result in a search query.
        /// </summary>
        /// <param name="indexDefinition"></param>
        /// <param name="query"></param>
        /// <param name="docId"></param>
        /// <returns></returns>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest, ResponseFormat = WebMessageFormat.Json)]
        public Explanation Explain(BaristaIndexDefinition indexDefinition, Barista.Search.Query query, int docId)
        {
            var lQuery = Barista.Search.Query.ConvertQueryToLuceneQuery(query);
            Explanation explanation;

            var index = GetOrAddIndex(indexDefinition, true);
            IndexSearcher indexSearcher;
            using (index.GetSearcher(out indexSearcher))
            {
                var lexplanation = indexSearcher.Explain(lQuery, docId);
                explanation = Explanation.ConvertLuceneExplanationToExplanation(lexplanation);
            }

            return explanation;
        }

        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest, ResponseFormat = WebMessageFormat.Json)]
        public ICollection<string> GetFieldNames(BaristaIndexDefinition indexDefinition)
        {
            var index = GetOrAddIndex(indexDefinition, true);
            IndexSearcher indexSearcher;
            using (index.GetSearcher(out indexSearcher))
            {
                var fieldNames = indexSearcher.IndexReader.GetFieldNames(IndexReader.FieldOption.ALL);
                return fieldNames;
            }
        }

        /// <summary>
        /// Returns a highlighted string for the specified query, results doc id, fieldname and fragment size.
        /// </summary>
        /// <param name="indexDefinition"></param>
        /// <param name="query"></param>
        /// <param name="docId"></param>
        /// <param name="fieldName"></param>
        /// <param name="fragCharSize"></param>
        /// <returns></returns>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest, ResponseFormat = WebMessageFormat.Json)]
        public string Highlight(BaristaIndexDefinition indexDefinition, Barista.Search.Query query, int docId, string fieldName, int fragCharSize)
        {
            var highlighter = GetFastVectorHighlighter();
            var lQuery = Barista.Search.Query.ConvertQueryToLuceneQuery(query);

            var fieldQuery = highlighter.GetFieldQuery(lQuery);
            string highlightedResult;

            var index = GetOrAddIndex(indexDefinition, true);
            IndexSearcher indexSearcher;
            using (index.GetSearcher(out indexSearcher))
            {
                highlightedResult = highlighter.GetBestFragment(fieldQuery, indexSearcher.IndexReader,
                                                                docId,
                                                                fieldName,
                                                                fragCharSize);
            }

            return highlightedResult;
        }

        /// <summary>
        /// Indexes the specified document.
        /// </summary>
        /// <param name="indexDefinition"></param>
        /// <param name="documentId"></param>
        /// <param name="document"></param>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest)]
        public void IndexDocument(BaristaIndexDefinition indexDefinition, string documentId, DocumentDto document)
        {
            try
            {
                if (documentId.IsNullOrWhiteSpace())
                    throw new ArgumentNullException("documentId", @"A document id must be specified.");

                if (document == null)
                    throw new ArgumentNullException("document", @"A document must be specified.");

                var index = GetOrAddIndex(indexDefinition, true);

                try
                {
                    //Add it to the index.
                    var luceneDocument = DocumentDto.ConvertToLuceneDocument(document);

                    var batch = new IndexingBatch();
                    batch.Add(new BatchedDocument
                      {
                          DocumentId = documentId,
                          Document = luceneDocument,
                          SkipDeleteFromIndex = false,
                      });

                    index.IndexDocuments(batch);
                }
                catch (OutOfMemoryException)
                {
                    CloseIndexWriter(indexDefinition, false);
                }
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.Message);
            }
        }

        /// <summary>
        /// Indexes the specified document.
        /// </summary>
        /// <param name="indexDefinition"></param>
        /// <param name="document"></param>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest)]
        public void IndexJsonDocument(BaristaIndexDefinition indexDefinition, JsonDocumentDto document)
        {
            try
            {
                if (document == null)
                    throw new ArgumentNullException("document", @"A document must be specified.");

                if (document.DocumentId.IsNullOrWhiteSpace())
                    throw new InvalidOperationException(@"The json document must specify a document id.");

                IndexJsonDocuments(indexDefinition, new List<JsonDocumentDto> { document });
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.Message);
            }
        }

        /// <summary>
        /// Indexes the specified documents.
        /// </summary>
        /// <param name="indexDefinition"></param>
        /// <param name="documents"></param>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest)]
        public void IndexJsonDocuments(BaristaIndexDefinition indexDefinition, IEnumerable<JsonDocumentDto> documents)
        {
            try
            {
                if (documents == null)
                    throw new ArgumentNullException("documents", @"A collection of documents must be specified.");

                var jsonDocuments = documents as IList<JsonDocumentDto> ?? documents.ToList();

                if (jsonDocuments.Any() == false)
                    throw new ArgumentNullException("documents", @"At least one document must be contained within the collection.");

                var index = GetOrAddIndex(indexDefinition, true);

                try
                {
                    //Add it to the index.
                    var batch = new IndexingBatch();

                    //Update the indexDefinition for the index based on the options specified.
                    foreach (var document in jsonDocuments)
                        UpdateIndexDefinitionFromFieldOptions(index.IndexDefinition, document.FieldOptions);

                    //Attempt to create a new Search.JsonDocument from the document
                    var searchJsonDocuments = jsonDocuments.Select(document => new Search.JsonDocument
                    {
                        DocumentId = document.DocumentId,
                        Metadata = document.MetadataAsJson.IsNullOrWhiteSpace() == false
                                     ? JObject.Parse(document.MetadataAsJson)
                                     : new JObject(),
                        DataAsJson = JObject.Parse(document.DataAsJson)
                    });

                    var luceneDocuments =
                      JsonDocumentToLuceneDocumentConverter.ConvertJsonDocumentToLuceneDocument(index.IndexDefinition,
                                                                                                searchJsonDocuments);

                    foreach (var luceneDocument in luceneDocuments)
                    {
                        batch.Add(luceneDocument);
                    }

                    //TODO: Add the batch to a BlockingCollection<IndexingBatch> and run a thread that consumes the batches
                    //See http://www.codethinked.com/blockingcollection-and-iproducerconsumercollection
                    index.IndexDocuments(batch);
                }
                catch (OutOfMemoryException)
                {
                    CloseIndexWriter(indexDefinition, false);
                }
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.Message);
            }
        }

        /// <summary>
        /// Retrieves the document with the corresponding document id.
        /// </summary>
        /// <param name="indexDefinition"></param>
        /// <param name="documentId"></param>
        /// <returns></returns>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest)]
        public JsonDocumentDto Retrieve(BaristaIndexDefinition indexDefinition, string documentId)
        {
            if (documentId.IsNullOrWhiteSpace())
                throw new ArgumentNullException("documentId", @"A document Id must be specified.");

            try
            {
                var index = GetOrAddIndex(indexDefinition, true);
                IndexSearcher indexSearcher;
                using (index.GetSearcher(out indexSearcher))
                {
                    var term = new Lucene.Net.Index.Term(Constants.DocumentIdFieldName, documentId.ToLowerInvariant());

                    var termQuery = new Lucene.Net.Search.TermQuery(term);

                    var hits = indexSearcher.Search(termQuery, 1);

                    if (hits.TotalHits == 0)
                        return null;

                    var result = RetrieveSearchResults(indexSearcher, hits).FirstOrDefault();

                    return result == null
                      ? null
                      : result.Document;
                }
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.Message);
            }
        }

        /// <summary>
        /// Returns documents that match the specified lucene query, limiting to the specified number of items.
        /// </summary>
        /// <param name="indexDefinition"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest, ResponseFormat = WebMessageFormat.Json)]
        public IList<SearchResult> Search(BaristaIndexDefinition indexDefinition, SearchArguments arguments)
        {
            if (arguments == null)
                arguments = new SearchArguments();

            try
            {
                var index = GetOrAddIndex(indexDefinition, true);
                var searchParams = GetLuceneSearchParams(arguments);

                IndexSearcher indexSearcher;
                using (index.GetSearcher(out indexSearcher))
                {
                    if (searchParams.Skip.HasValue == false)
                    {
                        var hits = indexSearcher.Search(searchParams.Query, searchParams.Filter, searchParams.MaxResults, searchParams.Sort);
                        return RetrieveSearchResults(indexSearcher, hits);
                    }
                    else
                    {
                        var hits = indexSearcher.Search(searchParams.Query, searchParams.Filter, searchParams.MaxResults + searchParams.Skip.Value, searchParams.Sort);
                        return RetrieveSearchResults(indexSearcher, hits)
                          .Skip(searchParams.Skip.Value)
                          .Take(searchParams.MaxResults)
                          .ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.Message);
            }
        }

        /// <summary>
        /// Returns a value that indicates the number of documents that match the specified lucene query.
        /// </summary>
        /// <param name="indexDefinition"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest, ResponseFormat = WebMessageFormat.Json)]
        public int SearchResultCount(BaristaIndexDefinition indexDefinition, SearchArguments arguments)
        {
            if (arguments == null)
                arguments = new SearchArguments();

            try
            {
                var index = GetOrAddIndex(indexDefinition, true);
                var searchParams = GetLuceneSearchParams(arguments);

                IndexSearcher indexSearcher;
                using (index.GetSearcher(out indexSearcher))
                {
                    if (searchParams.Skip.HasValue == false)
                    {
                        var hits = indexSearcher.Search(searchParams.Query, searchParams.Filter, searchParams.MaxResults, searchParams.Sort);
                        return hits.ScoreDocs.Count();
                    }
                    else
                    {
                        var hits = indexSearcher.Search(searchParams.Query, searchParams.Filter, searchParams.MaxResults + searchParams.Skip.Value, searchParams.Sort);
                        return hits.ScoreDocs
                                   .Skip(searchParams.Skip.Value)
                                   .Take(searchParams.MaxResults)
                                   .Count();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.Message);
            }
        }

        /// <summary>
        /// Performs a faceted search with the specified search arguments using the specified index.
        /// </summary>
        /// <param name="indexDefinition"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest, ResponseFormat = WebMessageFormat.Json)]
        public IList<FacetedSearchResult> FacetedSearch(BaristaIndexDefinition indexDefinition, SearchArguments arguments)
        {
            if (arguments == null)
                arguments = new SearchArguments();

            try
            {
                var index = GetOrAddIndex(indexDefinition, true);
                var searchParams = GetLuceneSearchParams(arguments);

                IndexSearcher indexSearcher;
                using (index.GetSearcher(out indexSearcher))
                {
                    var reader = indexSearcher.IndexReader;

                    if (arguments.GroupByFields == null)
                        arguments.GroupByFields = new List<string>();

                    var facetedSearch = new SimpleFacetedSearch(reader, arguments.GroupByFields.ToArray());
                    var hits = facetedSearch.Search(searchParams.Query, searchParams.MaxResults);
                    var result = hits.HitsPerFacet
                                     .AsQueryable()
                                     .OrderByDescending(hit => hit.HitCount)
                                     .Select(facetHits => RetrieveFacetSearchResults(facetHits))
                                     .ToList();
                    return result;
                }
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.Message);
            }
        }

        /// <summary>
        /// Sets field options
        /// </summary>
        /// <param name="indexDefinition"></param>
        /// <param name="fieldOptions"></param>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest)]
        public void SetFieldOptions(BaristaIndexDefinition indexDefinition, IEnumerable<FieldOptions> fieldOptions)
        {
            try
            {
                var index = GetOrAddIndex(indexDefinition, true);
                var luceneIndexDefinition = index.IndexDefinition;
                foreach (var fieldOption in fieldOptions)
                {
                    if (fieldOption.FieldName.IsNullOrWhiteSpace())
                        continue;

                    if (fieldOption.Index.HasValue)
                    {
                        var fieldIndexingType = MapFieldIndexTypeToFieldIndexing(fieldOption.Index.Value);

                        if (luceneIndexDefinition.Indexes.ContainsKey(fieldOption.FieldName))
                            luceneIndexDefinition.Indexes[fieldOption.FieldName] = fieldIndexingType;
                        else
                            luceneIndexDefinition.Indexes.Add(fieldOption.FieldName, fieldIndexingType);
                    }

                    if (fieldOption.Storage.HasValue)
                    {
                        var fieldStorageType = MapFieldStorageTypeToFieldStorage(fieldOption.Storage.Value);

                        if (luceneIndexDefinition.Stores.ContainsKey(fieldOption.FieldName))
                            luceneIndexDefinition.Stores[fieldOption.FieldName] = fieldStorageType;
                        else
                            luceneIndexDefinition.Stores.Add(fieldOption.FieldName, fieldStorageType);
                    }

                    if (fieldOption.TermVectorType.HasValue)
                    {
                        var fieldTermVector = MapFieldTermVectorTypeToFieldTermVector(fieldOption.TermVectorType.Value);

                        if (luceneIndexDefinition.TermVectors.ContainsKey(fieldOption.FieldName))
                            luceneIndexDefinition.TermVectors[fieldOption.FieldName] = fieldTermVector;
                        else
                            luceneIndexDefinition.TermVectors.Add(fieldOption.FieldName, fieldTermVector);
                    }

                }
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.Message);
            }
        }

        /// <summary>
        /// Shutsdown (flushes and closes) the specified index.
        /// </summary>
        /// <param name="indexDefinition"></param>
        [OperationBehavior(Impersonation = ImpersonationOption.Allowed)]
        [WebInvoke(Method = "POST", BodyStyle = WebMessageBodyStyle.WrappedRequest)]
        public void Shutdown(BaristaIndexDefinition indexDefinition)
        {
            try
            {
                CloseIndexWriter(indexDefinition, true);
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.Message);
            }
        }

        protected virtual Lucene.Net.Store.Directory GetLuceneDirectoryImplementationFromType(Type directoryType, BaristaIndexDefinition indexDefinition)
        {
            //TODO: Use Ninject here...

            if (directoryType == typeof(Lucene.Net.Store.SimpleFSDirectory))
            {
                var di = new DirectoryInfo(indexDefinition.IndexStoragePath);
                if (di.Exists == false)
                    di.Create();

                return new Lucene.Net.Store.SimpleFSDirectory(di);
            }

            if (directoryType == typeof(Lucene.Net.Store.RAMDirectory))
            {
                return new Lucene.Net.Store.RAMDirectory();
            }

            //A little bit of extensibility...
            var directory = (Lucene.Net.Store.Directory)Activator.CreateInstance(directoryType, indexDefinition.IndexStoragePath);
            return directory;
        }

        protected virtual Lucene.Net.Store.Directory GetLuceneDirectoryFromIndexDefinition(BaristaIndexDefinition indexDefinition)
        {
            //Lets create the Directory object from the index definition!!
            var directoryType = Type.GetType(indexDefinition.TypeName, false, true);
            if (directoryType == null)
                throw new InvalidOperationException(
                  string.Format("An index definition named {0} was located, however, the type {1} could not be found.",
                                indexDefinition.IndexName, indexDefinition.TypeName));

            if (typeof(Lucene.Net.Store.Directory).IsAssignableFrom(directoryType) == false)
                throw new InvalidOperationException(
                  string.Format("An index definition named {0} was located, however, the type {1} is not a directory type.",
                                indexDefinition.IndexName, indexDefinition.TypeName));

            return GetLuceneDirectoryImplementationFromType(directoryType, indexDefinition);
        }

        private static void UpdateIndexDefinitionFromFieldOptions(IndexDefinition indexDefinition, IEnumerable<FieldOptions> fieldOptions)
        {
            if (fieldOptions == null)
                return;

            foreach (var fieldOption in fieldOptions)
            {

                if (fieldOption.Index.HasValue)
                {
                    var indexOption = MapFieldIndexTypeToFieldIndexing(fieldOption.Index.Value);
                    if (indexDefinition.Indexes.ContainsKey(fieldOption.FieldName) == false)
                        indexDefinition.Indexes.Add(fieldOption.FieldName, indexOption);
                    else
                        indexDefinition.Indexes[fieldOption.FieldName] = indexOption;
                }

                if (fieldOption.Storage.HasValue)
                {
                    var storageOption = MapFieldStorageTypeToFieldStorage(fieldOption.Storage.Value);
                    if (indexDefinition.Stores.ContainsKey(fieldOption.FieldName) == false)
                        indexDefinition.Stores.Add(fieldOption.FieldName, storageOption);
                    else
                        indexDefinition.Stores[fieldOption.FieldName] = storageOption;
                }

                if (fieldOption.TermVectorType.HasValue)
                {
                    var termVector = MapFieldTermVectorTypeToFieldTermVector(fieldOption.TermVectorType.Value);
                    if (indexDefinition.TermVectors.ContainsKey(fieldOption.FieldName) == false)
                        indexDefinition.TermVectors.Add(fieldOption.FieldName, termVector);
                    else
                        indexDefinition.TermVectors[fieldOption.FieldName] = termVector;
                }
            }
        }

        /// <summary>
        /// Utility method to retrieve the specifed index, optionally creating the index if it is missing.
        /// </summary>
        /// <param name="indexDefinition"></param>
        /// <param name="createIfMissing"></param>
        /// <returns></returns>
        protected Index GetOrAddIndex(BaristaIndexDefinition indexDefinition, bool createIfMissing)
        {
            //Index name is case insensitive.
            indexDefinition.IndexName = indexDefinition.IndexName.ToLowerInvariant();

            return Indexes.GetOrAdd(indexDefinition, key =>
              {
                  var targetDirectory = GetLuceneDirectoryFromIndexDefinition(indexDefinition);

                  var luceneIndexDefinition = new IndexDefinition();

                  if (!IndexReader.IndexExists(targetDirectory))
                  {
                      if (createIfMissing == false)
                          throw new InvalidOperationException("Index does not exist: " + targetDirectory);

                      WriteIndexVersion(targetDirectory);

                      //creating index structure if we need to
                      new IndexWriter(targetDirectory, DummyAnalyzer, IndexWriter.MaxFieldLength.UNLIMITED).Dispose();
                  }
                  else
                  {
                      if (!EnsureIndexVersionMatches(luceneIndexDefinition.Name, targetDirectory))
                      {
                          CheckIndexAndRecover(targetDirectory, indexDefinition.IndexName);
                          targetDirectory.DeleteFile("writing-to-index.lock");
                          WriteIndexVersion(targetDirectory);
                      }

                      if (targetDirectory.FileExists("write.lock"))// force lock release, because it was still open when we shut down
                      {
                          IndexWriter.Unlock(targetDirectory);
                          // for some reason, just calling unlock doesn't remove this file
                          targetDirectory.DeleteFile("write.lock");
                      }

                      if (targetDirectory.FileExists("writing-to-index.lock")) // we had an unclean shutdown
                      {
                          //if (configuration.ResetIndexOnUncleanShutdown)
                          //  throw new InvalidOperationException("Rude shutdown detected on: " + indexDirectory);

                          CheckIndexAndRecover(targetDirectory, indexDefinition.IndexName);
                          targetDirectory.DeleteFile("writing-to-index.lock");
                      }
                  }

                  var simpleIndex = new SimpleIndex(targetDirectory, "DefaultIndex", luceneIndexDefinition);
                  return simpleIndex;
              });
        }

        /// <summary>
        /// Utility method to get a default FastVectorHighlighter.
        /// </summary>
        /// <returns></returns>
        private static FastVectorHighlighter GetFastVectorHighlighter()
        {
            FragListBuilder fragListBuilder = new SimpleFragListBuilder();
            FragmentsBuilder fragmentBuilder = new ScoreOrderFragmentsBuilder(BaseFragmentsBuilder.COLORED_PRE_TAGS, BaseFragmentsBuilder.COLORED_POST_TAGS);
            return new FastVectorHighlighter(true, true, fragListBuilder, fragmentBuilder);
        }

        private static LuceneParams GetLuceneSearchParams(SearchArguments arguments)
        {
            var result = new LuceneParams();

            //Set Defaults
            if (arguments.Query == null)
                arguments.Query = new MatchAllDocsQuery();

            //Special Behavior for OData Queries since OData queries potentially specify the query/filter/skip/take all in one.
            var query = arguments.Query as ODataQuery;
            if (query != null)
            {
                var oDataQuery = query;
                var parser = new ODataQueryParser();
                var modelFilter = parser.ParseQuery(oDataQuery.DefaultField, oDataQuery.Query);

                result.Query = modelFilter.Query.IsNullOrWhiteSpace()
                           ? new Lucene.Net.Search.MatchAllDocsQuery()
                           : LuceneModelFilter.ParseQuery(oDataQuery.DefaultField, modelFilter.Query);

                result.Filter = modelFilter.Filter.IsNullOrWhiteSpace()
                            ? null
                            : LuceneModelFilter.ParseFilter(oDataQuery.DefaultField, modelFilter.Filter);

                result.Sort = modelFilter.Sort ?? new Lucene.Net.Search.Sort();

                if (modelFilter.Take > 0)
                    result.MaxResults = modelFilter.Take;

                if (modelFilter.Skip > 0)
                    result.Skip = modelFilter.Skip;
            }
            else
            {
                result.Query = Barista.Search.Query.ConvertQueryToLuceneQuery(arguments.Query);
                result.Filter = null;
                result.Sort = Barista.Search.Sort.ConvertSortToLuceneSort(arguments.Sort);

                if (arguments.Filter != null)
                {
                    result.Filter = Barista.Search.Filter.ConvertFilterToLuceneFilter(arguments.Filter);
                }

                if (arguments.Skip.HasValue)
                    result.Skip = arguments.Skip.Value;

                result.MaxResults = arguments.Take ?? 1000;
            }

            return result;
        }

        private static IList<SearchResult> RetrieveSearchResults(IndexSearcher indexSearcher, TopDocs hits)
        {
            //iterate over the results.
            var results = hits.ScoreDocs.AsQueryable()
                       .OrderByDescending(hit => hit.Score)
                       .ToList()
                       .Select(hit =>
                       {
                           var jsonDocumentField = indexSearcher.Doc(hit.Doc).GetField(Constants.JsonDocumentFieldName);

                           var fieldDoc = (hit as FieldDoc);
                           if (fieldDoc != null && Double.IsNaN(hit.Score) && fieldDoc.fields.Length > 0 && fieldDoc.fields[0] is float)
                               hit.Score = (float)fieldDoc.fields[0]; //TODO: is this really true?

                           if (jsonDocumentField == null)
                               return new SearchResult
                               {
                                   Score = hit.Score,
                                   LuceneDocId = hit.Doc,
                                   Document = null
                               };

                           return new SearchResult
                           {
                               Score = hit.Score,
                               LuceneDocId = hit.Doc,
                               Document = JsonConvert.DeserializeObject<JsonDocumentDto>(jsonDocumentField.StringValue)
                           };
                       })
                       .ToList();

            return results;
        }

        private static FacetedSearchResult RetrieveFacetSearchResults(SimpleFacetedSearch.HitsPerFacet hits)
        {
            var result = new FacetedSearchResult
              {
                  FacetName = hits.Name.ToString(),
                  HitCount = hits.HitCount,
                  Documents = hits.Select(hit =>
                    {
                        var jsonDocumentField = hit.GetField(Constants.JsonDocumentFieldName);

                        if (jsonDocumentField == null)
                            return new SearchResult
                              {
                                  Score = 0,
                                  LuceneDocId = 0,
                                  Document = null
                              };

                        return new SearchResult
                          {
                              Score = 0,
                              LuceneDocId = 0,
                              Document = JsonConvert.DeserializeObject<JsonDocumentDto>(jsonDocumentField.StringValue)
                          };
                    }).ToList()
              };
            return result;
        }

        private static bool EnsureIndexVersionMatches(string indexName, Lucene.Net.Store.Directory directory)
        {
            if (directory.FileExists("index.version") == false)
            {
                return false;
                //throw new InvalidOperationException("Could not find index.version " + indexName + ", resetting index");
            }
            using (var indexInput = directory.OpenInput("index.version"))
            {
                var versionFromDisk = indexInput.ReadString();
                if (versionFromDisk != IndexVersion)
                    throw new InvalidOperationException("Index " + indexName + " is of version " + versionFromDisk +
                                      " which is not compatible with " + IndexVersion + ", resetting index");
            }

            return true;
        }

        /// <summary>
        /// Attempts to check and recover the specified Lucene Directory.
        /// </summary>
        /// <param name="directory">The directory to check and recover.</param>
        /// <param name="indexName">Name of the index -- Only used for logging.</param>
        private static void CheckIndexAndRecover(Lucene.Net.Store.Directory directory, string indexName)
        {
            StartupLog.Warn("Unclean shutdown detected on {0}, checking the index for errors. This may take a while.", indexName);

            var memoryStream = new MemoryStream();
            var stringWriter = new StreamWriter(memoryStream);
            var checkIndex = new CheckIndex(directory);

            if (StartupLog.IsWarnEnabled)
                checkIndex.SetInfoStream(stringWriter);

            var sp = Stopwatch.StartNew();
            var status = checkIndex.CheckIndex_Renamed_Method();
            sp.Stop();
            if (StartupLog.IsWarnEnabled)
            {
                StartupLog.Warn("Checking index {0} took: {1}, clean: {2}", indexName, sp.Elapsed, status.clean);
                memoryStream.Position = 0;

                Log.Warn(new StreamReader(memoryStream).ReadToEnd());
            }

            if (status.clean)
                return;

            StartupLog.Warn("Attempting to fix index: {0}", indexName);
            sp.Stop();
            sp.Reset();
            sp.Start();
            checkIndex.FixIndex(status);
            StartupLog.Warn("Fixed index {0} in {1}", indexName, sp.Elapsed);
        }

        private static void WriteIndexVersion(Lucene.Net.Store.Directory directory)
        {
            using (var indexOutput = directory.CreateOutput("index.version"))
            {
                indexOutput.WriteString(IndexVersion);
                indexOutput.Flush();
                indexOutput.Dispose();
            }
        }

        private static FieldIndexing MapFieldIndexTypeToFieldIndexing(FieldIndexType indexType)
        {
            var fieldIndexingType = FieldIndexing.Analyzed;
            switch (indexType)
            {
                case FieldIndexType.Analyzed:
                case FieldIndexType.AnalyzedNoNorms:
                    fieldIndexingType = FieldIndexing.Analyzed;
                    break;
                case FieldIndexType.NotAnalyzed:
                case FieldIndexType.NotAnalyzedNoNorms:
                    fieldIndexingType = FieldIndexing.NotAnalyzed;
                    break;
                case FieldIndexType.NotIndexed:
                    fieldIndexingType = FieldIndexing.No;
                    break;
            }

            return fieldIndexingType;
        }

        private static FieldStorage MapFieldStorageTypeToFieldStorage(FieldStorageType fieldStorageType)
        {
            var fieldStorage = FieldStorage.Yes;

            switch (fieldStorageType)
            {
                case FieldStorageType.NotStored:
                    fieldStorage = FieldStorage.No;
                    break;
                case FieldStorageType.Stored:
                    fieldStorage = FieldStorage.Yes;
                    break;
            }

            return fieldStorage;
        }

        private static FieldTermVector MapFieldTermVectorTypeToFieldTermVector(FieldTermVectorType fieldTermVectorType)
        {
            var fieldTermVector = FieldTermVector.WithOffsets;

            switch (fieldTermVectorType)
            {
                case FieldTermVectorType.WithOffsets:
                    fieldTermVector = FieldTermVector.WithOffsets;
                    break;
                case FieldTermVectorType.WithPositions:
                    fieldTermVector = FieldTermVector.WithPositions;
                    break;
                case FieldTermVectorType.WithPositionsOffsets:
                    fieldTermVector = FieldTermVector.WithPositionsAndOffsets;
                    break;
                case FieldTermVectorType.Yes:
                    fieldTermVector = FieldTermVector.Yes;
                    break;
                case FieldTermVectorType.No:
                    fieldTermVector = FieldTermVector.No;
                    break;
            }

            return fieldTermVector;
        }

        public static void CloseAllIndexes()
        {
            foreach (var kvp in Indexes)
            {
                CloseIndexWriter(kvp.Key, true);
            }
        }

        public static void CloseIndexWriter(BaristaIndexDefinition indexDefinition, bool waitForMerges)
        {
            Index index;
            if (Indexes.TryRemove(indexDefinition, out index))
            {
                index.Dispose();
            }
        }

        public void Dispose()
        {
            CloseAllIndexes();
        }

        #region Nested Classes

        private class LuceneParams
        {
            public LuceneParams()
            {
                MaxResults = 10000;
                Skip = null;
            }

            public Lucene.Net.Search.Query Query
            {
                get;
                set;
            }

            public Lucene.Net.Search.Filter Filter
            {
                get;
                set;
            }

            public Lucene.Net.Search.Sort Sort
            {
                get;
                set;
            }

            public int MaxResults
            {
                get;
                set;
            }

            public int? Skip
            {
                get;
                set;
            }
        }

        #endregion
    }
}

namespace Barista.Search.OData2Lucene
{
  internal class FunctionTokenSet : TokenSet
  {
    public override string ToString()
    {
      return string.Format("{0} {1} {2}", Operation, Left, Right);
    }
  }
}
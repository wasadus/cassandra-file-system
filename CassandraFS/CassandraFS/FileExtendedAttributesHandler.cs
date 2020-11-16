using GroBuf;
using GroBuf.DataMembersExtracters;

namespace CassandraFS
{
    public static class FileExtendedAttributesHandler
    {
        private static readonly Serializer serializer = new Serializer(new PropertiesExtractor(), options : GroBufOptions.WriteEmptyObjects);

        public static byte[] SerializeExtendedAttributes(ExtendedAttributes xattrs) => serializer.Serialize(xattrs);

        public static ExtendedAttributes DeserializeExtendedAttributes(byte[] xattrs) =>
            serializer.Deserialize<ExtendedAttributes>(xattrs);
    }
}
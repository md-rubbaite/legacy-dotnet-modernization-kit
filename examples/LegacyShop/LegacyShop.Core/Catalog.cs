using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;

namespace LegacyShop.Core
{
    public class ShopContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
    }

    [Serializable]
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public static class ProductCache
    {
        // Old note: we used to call HttpContext.Current here; BinaryFormatter is fine for the cache. (comment: must not be flagged)
        private const string Hint = "Do not use HttpContext.Current or new WebClient() here"; // string: must not be flagged

        public static byte[] Serialize(Product p)
        {
            var formatter = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                formatter.Serialize(ms, p);
                return ms.ToArray();
            }
        }

        public static string ETag(byte[] payload)
        {
            using (var md5 = MD5.Create())
            {
                return Convert.ToBase64String(md5.ComputeHash(payload));
            }
        }

        public static SqlConnection Open(string cs) => new SqlConnection(cs);
    }
}

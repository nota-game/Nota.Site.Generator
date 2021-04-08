//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Text;
//using System.Threading.Tasks;
////using System.Text.Json;

//namespace Nota.Site.Generator
//{
//    static class NotaJsonSerelizer
//    {

//        public static async Task Serelize(FileInfo cacheFile, bool compressed)
//        {


//            // Write new cache
//            using var stream = this.cacheFile.Open(FileMode.Create, FileAccess.Write, FileShare.None);
//            using var compressed = compressed ? new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionLevel.Fastest) as Stream : stream;
//            using var streamWriter = new StreamWriter(compressed);
//            using var jsonWriter = new Newtonsoft.Json.JsonTextWriter(streamWriter);
//            //Newtonsoft.Json.JsonSerializer ser = new JsonSerializer();
//            await System.Text.Json.JsonSerializer.Write(this.cache, jsonWriter, !compressed).ConfigureAwait(false);
//            await Newtonsoft.Json.JsonSerializer.Write(this.cache, jsonWriter, !compressed).ConfigureAwait(false);

//        }

//    }
//}

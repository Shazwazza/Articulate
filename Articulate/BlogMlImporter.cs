//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Xml;
//using BlogML.Xml;
//using Newtonsoft.Json.Serialization;

//namespace Articulate
//{
//    public class BlogMlImporter
//    {

//        public void Import(string fileName)
//        {
//            if (!File.Exists(fileName))
//            {
//                throw new FileNotFoundException("File not found: " + fileName);
//            }

//            using (var s = File.OpenRead(fileName))
//            {
//                var result = BlogMLSerializer.Deserialize(s);
//                ImportAuthors(result.Authors);
//            }
//        }

//        private void ImportAuthors(IEnumerable<BlogMLAuthor> authors)
//        {
//            foreach (var author in authors)
//            {
//                author.
//            }
//        }

//    }
//}

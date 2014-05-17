using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using BlogML.Xml;
using Newtonsoft.Json.Serialization;
using Umbraco.Core;

namespace Articulate
{
    public class BlogMlImporter
    {
        private readonly ApplicationContext _applicationContext;

        public BlogMlImporter(ApplicationContext applicationContext)
        {
            _applicationContext = applicationContext;
        }

        public void Import(string fileName)
        {
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException("File not found: " + fileName);
            }

            using (var s = File.OpenRead(fileName))
            {
                var result = BlogMLSerializer.Deserialize(s);
                ImportAuthors(result.Authors);
            }
        }

        private void ImportAuthors(IEnumerable<BlogMLAuthor> authors)
        {
            var authorType = _applicationContext.Services.ContentTypeService.GetContentType("ArticulateAuthor");
            if (authorType == null)
            {
                throw new InvalidOperationException("Articulate is not installed properly, the ArticulateAuthor doc type could not be found");
            }
            var authorNodes = _applicationContext.Services.ContentService.GetContentOfContentType(authorType.Id).ToArray();
            
            foreach (var author in authors)
            {
                //first check by email
                var found = _applicationContext.Services.UserService.GetByEmail(author.Email);
                if (found != null)
                {
                    //check if an author node exists for this user
                    if (!authorNodes.Any(x => x.Name.InvariantEquals(found.Name)))
                    {
                        //we should create one
                        //TODO: We need to specify the articulate root node to import to in this post
                    }
                }
            }
        }

    }
}

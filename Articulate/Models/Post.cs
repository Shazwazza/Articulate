using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    public class Post : ThemedModel
    {
        public Post(RenderModel baseModel)
            : base(baseModel)
        {
        }

        public string BlogTitle
        {
            get { return Content.GetPropertyValue<string>("blogTitle", true); }
        }

        public string Author
        {
            get { return Content.GetPropertyValue<string>("author", true); }
        }

        public DateTime PublishedDate
        {
            get { return Content.GetPropertyValue<DateTime>("publishedDate"); }
        }
    }

    public class ThemedModel : IRenderModel
    {
        private readonly RenderModel _baseModel;

        public ThemedModel(RenderModel baseModel)
        {
            _baseModel = baseModel;
        }

        /// <summary>
        /// Returns the current theme
        /// </summary>
        public string Theme
        {
            get { return Content.GetPropertyValue<string>("theme", true); }
        }

        public IPublishedContent Content
        {
            get { return _baseModel.Content; }
        }
    }
}

using System;
using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Web;
using Umbraco.Web.Models;

namespace Articulate.Models
{
    public class BlogModel : MasterModel
    {
        public BlogModel(IPublishedContent content)
            : base(content)
        {
        }

        public PageModel Pages
        {
            get { return new PageModel(); }
        }

        public override string BlogTitle
        {
            get { return Content.GetPropertyValue<string>("blogTitle", true); }
        }

        public override string BlogDescription
        {
            get { return Content.GetPropertyValue<string>("blogDescription", true); }
        }
        
    }
}
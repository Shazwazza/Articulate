using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Umbraco.Web.Mvc;

public class MyTestController : SurfaceController
{
    [HttpPost]
    public ActionResult DoThis()
    {
        ViewBag.MyMessage = "hello world";
        return CurrentUmbracoPage();
        //return RedirectToCurrentUmbracoPage();
        //return RedirectToCurrentUmbracoUrl();
    }
}
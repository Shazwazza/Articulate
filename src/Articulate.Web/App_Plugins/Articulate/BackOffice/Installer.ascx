<%@ Control Language="C#" AutoEventWireup="true" Inherits="Articulate.Controls.Installer"  %>

<div>
    <img src="https://raw.githubusercontent.com/Shandem/Articulate/master/assets/Logo.png" alt="Articulate"/>
    <h3>Congrats! Articulate is now installed</h3>    
    <br/>
    <p>
        The installer has installed all of the required Umbraco nodes including some demo data. 
        You can either modify this demo data or simply remove it once you are comfortable with how Articulate works.
        The demo data includes: a blog post, an author, a category and a tag.
    </p>
    <p>
        To customize your blog navigate to the Content section and click on the Articulate 'Blog' node. Here you can customize
        the look and feel of your blog, including changing the <a href="https://github.com/Shandem/Articulate/wiki/Installed-Themes" target="_blank">theme</a>, adding Google analytics tracking, etc... If you want 
        comments enabled you should sign up for a <a href="https://disqus.com/" target="_blank">Disqus account</a> and ensure you enter your Disqus details on the 'Blog' node.
    </p>
    <p>
        Click <a href="https://github.com/Shandem/Articulate/wiki" target="_blank">Here</a> to view Articulate documentation.
    </p>
    <p>
        Click <a href="<%=BlogUrl %>" target="_blank">here</a> to view the installed blog
    </p>
</div>
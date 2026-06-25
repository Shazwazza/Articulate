      _       _              _                                                          
     | |     | |            | |                                                         
   __| | ___ | |_ _ __   ___| |_   _ __   _____      __                                 
  / _` |/ _ \| __| '_ \ / _ \ __| | '_ \ / _ \ \ /\ / /                                 
 | (_| | (_) | |_| | | |  __/ |_  | | | |  __/\ V  V /                                  
  \__,_|\___/ \__|_| |_|\___|\__| |_| |_|\___| \_/\_/   _                 _             
                 | |                                   | |               (_)            
  _   _ _ __ ___ | |__  _ __ __ _  ___ ___     _____  _| |_ ___ _ __  ___ _  ___  _ __  
 | | | | '_ ` _ \| '_ \| '__/ _` |/ __/ _ \   / _ \ \/ / __/ _ \ '_ \/ __| |/ _ \| '_ \ 
 | |_| | | | | | | |_) | | | (_| | (_| (_) | |  __/>  <| ||  __/ | | \__ \ | (_) | | | |
  \__,_|_| |_| |_|_.__/|_|  \__,_|\___\___/   \___/_/\_\\__\___|_| |_|___/_|\___/|_| |_|
                                                                                        

== Requirements ==
* Node LTS Version 24+
* Use a tool such as NVM (Node Version Manager) for your OS to help manage multiple versions of Node

== Node Version Manager tools ==
* https://github.com/coreybutler/nvm-windows
* https://github.com/nvm-sh/nvm
* https://docs.volta.sh/guide/getting-started

== Steps ==
* Open a terminal inside the `Client` folder
* Run `pnpm install` to install all the dependencies
* Run `pnpm run build` to build the project
* The build output is copied to `wwwroot/App_Plugins/Articulate/BackOffice/articulate.js`

== File Watching ==
* Add this Class Library Project as a project reference to an Umbraco Website project
* From the `Client` folder run the command `pnpm run watch` this will monitor the changes to the *.ts files and rebuild the project
* With the Umbraco website project running the Class Library Project *should* refresh the browser when the build is complete
* If the browser does not refresh, you can run the website with `dotnet watch --no-hot-reload` and manually refresh the browser. `no-hot-reload` is only required to avoid refreshes due to token authentication requests by the backoffice when signed in.

== Suggestion ==
* Use VSCode as the editor of choice as it has good tooling support for TypeScript and it will recommend a VSCode Extension for good Lit WebComponent completions

== Other Resources ==
* Umbraco Docs - https://docs.umbraco.com/umbraco-cms/customizing/overview


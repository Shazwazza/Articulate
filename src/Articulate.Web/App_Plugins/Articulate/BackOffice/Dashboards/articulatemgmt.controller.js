angular.module("umbraco").controller("Articulate.Dashboard.Management",
    function ($scope, umbRequestHelper, formHelper, fileManager, $http, $q) {

        $scope.aPage = {};
        $scope.aPage.nameLocked = true;
        $scope.aPage.loading = false;

        $scope.aDashboard = {};
        $scope.aDashboard.name = "Articulate";

        $scope.aDashboard.tabs = [
            {
                "id": 1200,
                "active": true,
                "label": "Import",
                "alias": "import",
                "properties": [
                    {
                        "serverSide": false,
                        "path": "../App_Plugins/Articulate/BackOffice/Dashboards/blogimporter.html",
                        "caption": "Articulate BlogMl Importer"
                    }]
            },
            {
                "id": 1201,
                "active": false,
                "label": "Export",
                "alias": "export",
                "properties": [
                    {
                        "serverSide": false,
                        "path": "../App_Plugins/Articulate/BackOffice/Dashboards/blogexporter.html",
                        "caption": "Articulate BlogMl Exporter"
                    }]
            },
            {
                "id": 1202,
                "active": false,
                "label": "Installer",
                "alias": "installer",
                "properties": [
                    {                     
                        "serverSide": false,
                        "path": "../App_Plugins/Articulate/BackOffice/Dashboards/datainstaller.html",
                        //"path": "~/App_Plugins/Articulate/BackOffice/installer.ascx",
                        "caption": ""
                    }]
            }
        ];

    });

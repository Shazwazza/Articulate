(function () {
    'use strict';

    function articulateOptionsManagementController($scope, $element, $timeout) {

        var vm = this;
        vm.viewState = "list";
        vm.selectedGroup = null;

        //vm.loaded = false;
        //vm.$onInit = onInit;
        //vm.$onChanges = onChanges;
        //vm.$postLink = postLink;
        //vm.$onDestroy = onDestroy;

        vm.openGroup = openGroup;
        vm.setViewState = setViewState;

        function setViewState(state) {
            vm.viewState = state;
        }

        function openGroup(group) {
            vm.viewState = "details";
            vm.selectedGroup = group;
        }

        

        vm.groups = [
            {
                "name": "Articulate BlogMl Importer",
                "view": "../App_Plugins/Articulate/BackOffice/PackageOptions/blogimporter.html",
                "icon": "icon-download-alt"
            },
            {
                "name": "Articulate BlogMl Exporter",
                "view": "../App_Plugins/Articulate/BackOffice/PackageOptions/blogexporter.html",
                "icon": "icon-out"
            },
            {
                "name": "Articulate Data Installer",
                "view": "../App_Plugins/Articulate/BackOffice/PackageOptions/datainstaller.html",
                "icon": "icon-stacked-disks"
            }
        ];

    }

    var articulateOptionsMgmtComponent = {
        templateUrl: '../../App_Plugins/Articulate/BackOffice/PackageOptions/articulatemgmt.html',
        bindings: {

        },
        controllerAs: 'vm',
        controller: articulateOptionsManagementController
    };

    angular.module("umbraco")
        .component('articulateOptionsMgmt', articulateOptionsMgmtComponent);

})();
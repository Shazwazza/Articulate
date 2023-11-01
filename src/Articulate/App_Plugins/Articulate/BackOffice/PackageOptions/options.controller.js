(function () {
    "use strict";

    function ArticulateOptionsController($routeParams) {
        
        var vm = this;

        vm.justInstalled = $routeParams.packageId;

        vm.blogUrl = "";
    }

    angular.module("umbraco").controller("Articulate.Options.Main", ArticulateOptionsController);
})();
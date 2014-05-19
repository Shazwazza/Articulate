angular.module("umbraco").controller("Articulate.Dashboard.BlogImporter",
    function ($scope, umbRequestHelper) {

        var file = null;

        $scope.$on("filesSelected", function(e, args) {
            file = args.files[0];
        });

        $scope.submit = function() {
            var url = Umbraco.Sys.ServerVariables["articulate"]["articulateImportBaseUrl"] + "ImportBlogMl";

            umbRequestHelper.postMultiPartRequest(
                url,
                //this is just dummy data, but we need it because something needs to be there for validation purposes
                { key: "articulateNode", value: $scope.articulateNodeId },
                function (data, formData) {
                    //assign the file data to the request
                    formData.append(file.name, file);
                },
                function (data, status, headers, config) {
                    alert("sucess!");
                },
                function (data, status, headers, config) {
                    alert("failed!");
                });
        }
    });
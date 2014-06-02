angular.module("umbraco").controller("Articulate.Dashboard.BlogImporter",
    function ($scope, umbRequestHelper, formHelper, fileManager) {

        var file = null;

        $scope.submitting = false;
        
        $scope.$on("filesSelected", function(e, args) {
            file = args.files[0];        
        });

        $scope.submit = function() {

            if (formHelper.submitForm({ scope: $scope })) {

                formHelper.resetForm({ scope: $scope });

                $scope.submitting = true;

                var url = Umbraco.Sys.ServerVariables["articulate"]["articulateImportBaseUrl"] + "ImportBlogMl";

                umbRequestHelper.postMultiPartRequest(
                    url,
                    [
                        { key: "articulateNode", value: $scope.articulateNodeId },
                        { key: "overwrite", value: $scope.overwrite },
                        { key: "regexMatch", value: $scope.regexMatch },
                        { key: "regexReplace", value: $scope.regexReplace },
                        { key: "publish", value: $scope.publish }
                    ],
                    function(data, formData) {
                        //assign the file data to the request
                        formData.append(file.name, file);
                    },
                    function(data, status, headers, config) {
                        alert("Success!");
                        $scope.submitting = false;
                    },
                    function(data, status, headers, config) {
                        alert("failed :(");
                        $scope.submitting = false;
                    });
            }
        }
    }).directive('requiredFile', function () {
        return {
            require: 'ngModel',
            link: function (scope, el, attrs, ngModel) {

                ngModel.$setValidity("required", false);

                scope.$on("filesSelected", function (e, args) {
                    ngModel.$setValidity("required", true);
                });
            }
        }
    });
angular.module("umbraco").controller("Articulate.Dashboard.BlogExporter",
    function ($scope, umbRequestHelper, formHelper, fileManager, $http, $q) {
       
        function postExport() {
            return umbRequestHelper.resourcePromise(
                $http.post(
                    Umbraco.Sys.ServerVariables["articulate"]["articulateImportBaseUrl"] + "PostExportBlogMl", {
                        articulateNode: $scope.contentPickerExportModel.value
                    }),
                'Failed to export blog posts');
        }        

        $scope.submitting = false;

        $scope.contentPickerExportModel = {
            view: "contentpicker",
            config: {
                minNumber: 1
            }
        };

        $scope.submitExport = function () {

            $scope.status = "";

            if (formHelper.submitForm({ scope: $scope, formCtrl: $scope.articulateExportForm })) {

                formHelper.resetForm({ scope: $scope });

                $scope.submitting = true;
                $scope.status = "Please wait...";

                postExport()
                    .then(function (data) {

                        $scope.downloadLink = data.downloadUrl;

                        $scope.status = "Finished!";
                        $scope.submitting = false;
                    });
            }
        }
        
    });

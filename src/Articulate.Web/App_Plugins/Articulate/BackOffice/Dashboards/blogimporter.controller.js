angular.module("umbraco").controller("Articulate.Dashboard.BlogImporter",
    function ($scope, umbRequestHelper, formHelper, fileManager, $http, $q) {

        //initialize the import, this will upload the file and return the post count
        function postInitialize() {
            var deferred = $q.defer();
            umbRequestHelper.postMultiPartRequest(
                    Umbraco.Sys.ServerVariables["articulate"]["articulateImportBaseUrl"] + "PostInitialize",
                    //something needs to be here (dummy data)
                    { key: "temp", value: "nothing" },
                    function (data, formData) {
                        //assign the file data to the request
                        formData.append(file.name, file);
                    },
                    function (data, status, headers, config) {
                        $scope.status = "Please wait... Importing " + data.count + " blog posts...";
                        deferred.resolve(data.tempFile);
                    },
                    function (data, status, headers, config) {
                        deferred.reject('Failed to initialize');
                    });
            return deferred.promise;
        }

        function postImport(tempFile) {
            return umbRequestHelper.resourcePromise(
                $http.post(
                    Umbraco.Sys.ServerVariables["articulate"]["articulateImportBaseUrl"] + "PostImportBlogMl", {
                        articulateNode: $scope.contentPickerImportModel.value,
                        overwrite: $scope.overwrite,
                        regexMatch: $scope.regexMatch,
                        regexReplace: $scope.regexReplace,
                        publish: $scope.publish,
                        tempFile: tempFile,
                        exportDisqusXml: $scope.exportDisqusXml
                    }),
                'Failed to import blog posts');
        }
        

        var file = null;
        
        $scope.submitting = false;
        
        $scope.contentPickerImportModel = {
            view: "contentpicker",
            config: {
                minNumber: 1
            }
        };

        $scope.$on("filesSelected", function (e, args) {
            file = args.files[0];
        });
        

        $scope.submitImport = function () {

            $scope.status = "";

            if (formHelper.submitForm({ scope: $scope, formCtrl: $scope.articulateImportForm })) {

                formHelper.resetForm({ scope: $scope });

                $scope.submitting = true;
                $scope.status = "Please wait...";

                postInitialize()
                    .then(postImport)
                    .then(function (data) {

                        $scope.downloadLink = data.downloadUrl;

                        $scope.status = "Finished!";
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

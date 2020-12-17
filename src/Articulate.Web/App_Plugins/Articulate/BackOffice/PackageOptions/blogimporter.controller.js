angular.module("umbraco").controller("Articulate.Dashboard.BlogImporter",
    function ($scope, umbRequestHelper, formHelper, fileManager, $http, $q) {

        var vm = this;

        //initialize the import, this will upload the file and return the post count
        function postInitialize() {
            return umbRequestHelper.postMultiPartRequest(
                Umbraco.Sys.ServerVariables["articulate"]["articulateImportBaseUrl"] + "PostInitialize",
                //something needs to be here (dummy data)
                { key: "temp", value: "nothing" },
                function (data, formData) {
                    //assign the file data to the request
                    formData.append(file.name, file);
                }).then(
                    function (data, status, headers, config) {
                        vm.status = "Please wait... Importing " + data.data.count + " blog posts...";
                        return $q.resolve(data.data.tempFile);
                    },
                    function (data, status, headers, config) {
                        return $q.reject('Failed to initialize');
                    });
        }

        function postImport(tempFile) {
            return umbRequestHelper.resourcePromise(
                $http.post(
                    Umbraco.Sys.ServerVariables["articulate"]["articulateImportBaseUrl"] + "PostImportBlogMl", {
                        articulateNode: vm.contentPickerImportModel.value,
                        overwrite: vm.overwrite,
                        regexMatch: vm.regexMatch,
                        regexReplace: vm.regexReplace,
                        publish: vm.publish,
                        tempFile: tempFile,
                        exportDisqusXml: vm.exportDisqusXml,
                        importFirstImage: vm.importFirstImage
                    }),
                'Failed to import blog posts');
        }


        var file = null;

        vm.buttonState = "init";

        vm.contentPickerImportModel = {
            view: "contentpicker",
            label: "Articulate blog node",
            description: "Choose the Articulate blog node to import to",
            config: {
                minNumber: 1
            }
        };

        $scope.$on("filesSelected", function (e, args) {
            file = args.files[0];
        });

        $scope.$watch("vm.contentPickerImportModel.value", function (newVal, oldVal) {
            if (vm.articulateImportForm.articulateImportNodeId) {
                vm.articulateImportForm.articulateImportNodeId.$setValidity('required', newVal !== null && newVal !== undefined && newVal !== "");
            }
        });

        vm.toggleExport = function () {
            vm.exportDisqusXml = !vm.exportDisqusXml;
            vm.publish = vm.publish || vm.exportDisqusXml;
            vm.publish = vm.publish || vm.importFirstImage;
        }

        vm.toggleImportFirstImage = function () {
            vm.importFirstImage = !vm.importFirstImage;
            vm.publish = vm.publish || vm.exportDisqusXml;
            vm.publish = vm.publish || vm.importFirstImage;
        }

        vm.submitImport = function () {

            vm.status = "";

            if (formHelper.submitForm({ scope: $scope, formCtrl: vm.articulateImportForm })) {

                formHelper.resetForm({ scope: $scope, formCtrl: vm.articulateImportForm });

                vm.buttonState = "busy";
                vm.status = "Please wait...";

                postInitialize()
                    .then(postImport)
                    .then(function (data) {

                        vm.downloadLink = data.downloadUrl;

                        vm.status = "Finished!";
                        vm.buttonState = "success";
                    });
            }
            else {
                vm.buttonState = "error";
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

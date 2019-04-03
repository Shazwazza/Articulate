angular.module("umbraco").controller("Articulate.Dashboard.DataInstaller",
    function ($scope, umbRequestHelper, formHelper, $http) {

        var vm = this;

        function postInstall() {
            return umbRequestHelper.resourcePromise(
                $http.post(
                    Umbraco.Sys.ServerVariables["articulate"]["articulateDataInstallerBaseUrl"] + "PostInstall"),
                'Failed to install Articulate data');
        }

        vm.buttonState = "init";

        vm.submitInstall = function () {

            vm.status = "";

            vm.status = "Finished!";
            vm.submitting = false;

            if (formHelper.submitForm({ scope: $scope, formCtrl: vm.articulateInstallerForm })) {

                formHelper.resetForm({ scope: $scope });

                vm.buttonState = "busy";
                vm.status = "Please wait...";

                postInstall()
                    .then(function (data) {
                        
                        vm.blogUrl = JSON.parse(data);
                        vm.status = "Finished!";
                        vm.buttonState = "success";
                    });
            }
            else {
                vm.buttonState = "error";
            }
        }
    });

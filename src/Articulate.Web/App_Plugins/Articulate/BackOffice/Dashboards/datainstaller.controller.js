angular.module("umbraco").controller("Articulate.Dashboard.DataInstaller",
    function ($scope, umbRequestHelper, formHelper, fileManager, $http, $q) {

        function postInstall() {
            return umbRequestHelper.resourcePromise(
                $http.post(
                    Umbraco.Sys.ServerVariables["articulate"]["articulateDataInstallerBaseUrl"] + "PostInstall"),
                'Failed to install Articulate data');
        }

        

        $scope.submitInstall = function () {

            $scope.status = "";

            $scope.status = "Finished!";
            $scope.submitting = false;

            if (formHelper.submitForm({ scope: $scope, formCtrl: $scope.articulateInstallerForm })) {

                formHelper.resetForm({ scope: $scope });

                $scope.submitting = true;
                $scope.status = "Please wait...";

                postInstall()
                    .then(function (data) {
                        
                        $scope.blogUrl = JSON.parse(data);
                        $scope.status = "Finished!";
                        $scope.submitting = false;
                    });
            }
        }
    });

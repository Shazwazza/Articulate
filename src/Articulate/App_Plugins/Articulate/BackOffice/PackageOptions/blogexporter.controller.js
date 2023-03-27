angular.module("umbraco").controller("Articulate.Dashboard.BlogExporter",
  function ($scope, umbRequestHelper, formHelper, $http) {

    var vm = this;

    function postExport() {
      return umbRequestHelper.resourcePromise(
        $http.post(
          Umbraco.Sys.ServerVariables["articulate"]["articulateImportBaseUrl"] + "PostExportBlogMl", {
          articulateNode: vm.contentPickerExportModel.value,
          exportImagesAsBase64: vm.embedImages
        }),
        'Failed to export blog posts');
    }

    vm.buttonState = "init";

    vm.embedImages = false;
    vm.contentPickerExportModel = {
      view: "contentpicker",
      config: {
        minNumber: 1
      }
    };

    $scope.$watch("vm.contentPickerExportModel.value", function (newVal, oldVal) {
      if (vm.articulateExportForm.articulateExportNodeId) {
        vm.articulateExportForm.articulateExportNodeId.$setValidity('required', newVal !== null && newVal !== undefined && newVal !== "");
      }
    });

    vm.submitExport = function () {

      vm.status = "";

      if (formHelper.submitForm({ scope: $scope, formCtrl: vm.articulateExportForm })) {

        formHelper.resetForm({ scope: $scope, formCtrl: vm.articulateExportForm });

        vm.buttonState = "busy";
        vm.status = "Please wait...";

        postExport()
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

  });

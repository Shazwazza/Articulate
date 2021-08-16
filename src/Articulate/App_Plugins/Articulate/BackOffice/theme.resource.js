(function () {
    "use strict";

    function articulateThemeResource(umbRequestHelper, $http) {
        return {

            saveThemeFile: function (themeFile) {

                return umbRequestHelper.resourcePromise(
                    $http.post(
                        umbRequestHelper.getApiUrl(
                            "articulateThemeEditorApiBaseUrl",
                            "PostSaveThemeFile"), themeFile),
                    "Failed to delete theme");
            },

            deleteItem: function (id) {

                return umbRequestHelper.resourcePromise(
                    $http.post(
                        umbRequestHelper.getApiUrl(
                            "articulateThemeEditorApiBaseUrl",
                            "PostDeleteItem", { id: id })),
                    "Failed to delete theme");
            },

            copyTheme: function (themeName, copy) {

                return umbRequestHelper.resourcePromise(
                    $http.post(
                        umbRequestHelper.getApiUrl(
                            "articulateThemeEditorApiBaseUrl",
                            "PostCopyTheme", { themeName: themeName, copy: copy })),
                    "Failed to retrieve themes");
            },

            createFile: function (parentId, fileName, type) {

                return umbRequestHelper.resourcePromise(
                    $http.post(
                        umbRequestHelper.getApiUrl(
                            "articulateThemeEditorApiBaseUrl",
                            "PostCreateFile", { parentId, name: fileName, type: type })),
                    "Failed to retrieve themes");
            },

            getThemes: function (virtualpath) {

                return umbRequestHelper.resourcePromise(
                    $http.get(
                        umbRequestHelper.getApiUrl(
                            "articulateThemeEditorApiBaseUrl",
                            "GetThemes")),
                    "Failed to retrieve themes");
            },

            getByPath: function (virtualpath) {

                return umbRequestHelper.resourcePromise(
                    $http.get(
                        umbRequestHelper.getApiUrl(
                            "articulateThemeEditorApiBaseUrl",
                            "GetByPath",
                            { virtualPath: virtualpath })),
                    "Failed to retrieve data from virtual path " + virtualpath);
            }
        }
    }

    angular.module("umbraco").factory("articulateThemeResource", articulateThemeResource);


})();
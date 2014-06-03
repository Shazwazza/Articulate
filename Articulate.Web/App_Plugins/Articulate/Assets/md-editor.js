'use strict';

/* Controllers */

var articulateEditorApp = angular.module('articulateEditorApp', []);

articulateEditorApp.controller('EditorController', function ($scope) {

    $scope.phase = 0;
    $scope.title = "New Blog Post";
    $scope.md = "";

    $scope.next = function() {
        $scope.phase = 1;
    }
});
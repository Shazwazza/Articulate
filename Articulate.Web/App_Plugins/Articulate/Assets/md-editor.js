'use strict';

var articulateapp = angular.module('articulateapp', ['ngRoute', 'ngSanitize']);

articulateapp.config([
    '$routeProvider',
    function ($routeProvider) {
        $routeProvider.
            when('/md', {
                templateUrl: 'md.html',
                controller: function ($scope) {
                    $scope.$parent.caption = "New Blog Post";
                    $scope.$parent.nextPath = "/optional";
                    $scope.$parent.nextText = "&raquo;";
                    $scope.$parent.prevPath = null;
                }
            }).
            when('/optional', {
                templateUrl: 'optional.html',
                controller: function ($scope) {
                    $scope.$parent.caption = "Optional values";
                    $scope.$parent.nextPath = "/submit";
                    $scope.$parent.nextText = "Post it!";
                    $scope.$parent.prevPath = "/md";
                }
            }).
            when('/login', {
                templateUrl: 'login.html',
                controller: function ($scope, $http, $location) {
                    $scope.$parent.caption = "User login";
                    $scope.$parent.nextPath = null;
                    $scope.$parent.nextText = null;
                    $scope.$parent.prevPath = null;

                    $scope.username = "";
                    $scope.password = "";

                    $scope.login = function() {
                        $http.post($scope.$parent.doAuthUrl, {
                                username: $scope.username,
                                password: $scope.password
                            })
                            .success(function(data, status, headers, config) {
                                $location.path("/submit");
                            }).error(function(data, status, headers, config) {
                                $scope.failed = true;
                            });
                    }
                }
            }).
            when('/submit', {
                templateUrl: 'submit.html',
                controller: function ($scope, $location, $http) {

                    if ($scope.md.length === 0) {
                        $location.path("/md");
                        return;
                    }

                    $scope.$parent.caption = "Submitting post...";
                    $scope.$parent.nextPath = null;
                    $scope.$parent.nextText = null;
                    $scope.$parent.prevPath = null;

                    //check if they are auth'd
                    $http.get($scope.$parent.isAuthUrl)
                        .success(function (data, status, headers, config) {

                            if (data === "true") {
                                $http.post($scope.$parent.postUrl, {
                                        articulateNodeId: $scope.$parent.articulateNodeId,
                                        title: $scope.$parent.title,
                                        body: $scope.$parent.md,
                                        tags: $scope.$parent.tags,
                                        categories: $scope.$parent.categories,
                                        excerpt: $scope.$parent.excerpt,
                                        slug: $scope.$parent.slug
                                    })
                                    .success(function(data, status, headers, config) {
                                        $scope.result = data;

                                    }).error(function(data, status, headers, config) {
                                        if (data.Message) {
                                            alert(data.Message);
                                        }
                                        else {
                                            alert("Failed! " + angular.toJson(data));
                                        }
                                    });
                            }
                            else {
                                //need to login
                                $location.path("/login");
                            }
                        });
                }
            }).
            otherwise({
                redirectTo: '/md'
            });
    }
]);

articulateapp.controller('EditorController', function ($scope, $location, $element) {

    $scope.postUrl = $element.attr("data-articulate-post-url");
    $scope.isAuthUrl = $element.attr("data-umbraco-isauth-url");
    $scope.doAuthUrl = $element.attr("data-umbraco-doauth-url");
    $scope.articulateNodeId = $element.attr("data-articulate-nodeId");

    $scope.nextPath = null;
    $scope.prevPath = null;
    $scope.nextText = "&raquo;";
    $scope.prevText = "&laquo;";
    $scope.caption = "";
    $scope.title = "";
    $scope.md = "";
    $scope.tags = "";
    $scope.categories = "";
    $scope.excerpt = "";
    $scope.slug = "";

    $scope.go = function (p) {
        $scope.articulateForm.$setDirty();
        if ($scope.articulateForm.$valid) {
            $location.path(p);
        }
    }

});
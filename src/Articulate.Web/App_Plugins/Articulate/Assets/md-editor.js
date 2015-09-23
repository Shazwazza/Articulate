'use strict';

var articulateapp = angular.module('articulateapp', [
    'ngRoute',
    'ngSanitize']);

articulateapp.config([
    '$routeProvider',
    function ($routeProvider) {
        $routeProvider.
            when('/md', {
                templateUrl: 'md.html',
                controller: function ($scope, angularHelper) {
                    
                    function insertAtCaretPos($el, text, pos) {
                        var content = $el.val();
                        var newContent = content.substr(0, pos) + text + content.substr(pos);
                        $el.val(newContent);
                        $scope.$parent.md = newContent;
                    }

                    function getCaret(el) {
                        if (el.selectionStart) {
                            return el.selectionStart;
                        } else if (document.selection) {
                            el.focus();

                            var r = document.selection.createRange();
                            if (r == null) {
                                return 0;
                            }

                            var re = el.createTextRange(),
                                rc = re.duplicate();
                            re.moveToBookmark(r.getBookmark());
                            rc.setEndPoint('EndToStart', re);

                            return rc.text.length;
                        }
                        return 0;
                    }

                    $scope.caret = 0;
                    
                    //TODO: when the content has changed check if a file has been removed from the markup
                    // if it has we need to remove the file from the uploads as well - we also need to deal
                    // with that on the server side too.

                    $scope.addFile = function () {
                        $("#insertFile").click();
                    }

                    $scope.addCamera = function() {
                        $("#insertCamera").click();
                    }

                    $scope.storeCaret = function () {                        
                        var elem = $("#mdInput").get(0);
                        $scope.caret = getCaret(elem);
                    }

                    $scope.$on("filesSelected", function(e, o) {
                        if (o.files && o.files.length && o.files.length === 1) {
                            var file = o.files[0];

                            //TODO: validate that the file cannot contain a [ or ] char

                            //![Alt text](/path/to/img.jpg)

                            //NOTE: for some reason angular is not in a current apply when using events
                            // so we need to manually apply a digest here
                            angularHelper.safeApply($scope, function () {
                                //add to main file collection to upload
                                $scope.$parent.files.push(file);

                                var token = "[i:" + ($scope.$parent.files.length - 1) + ":" + file.name + "]";

                                insertAtCaretPos($("#mdInput"), token, $scope.caret);                                
                            });
                            
                        }
                    });

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

                    $scope.login = function () {

                        $scope.articulateForm.$setDirty();
                        if ($scope.articulateForm.$valid) {
                            $http.post($scope.$parent.doAuthUrl, {
                                username: $scope.username,
                                password: $scope.password
                            })
                            .success(function (data, status, headers, config) {
                                $location.path("/submit");
                            }).error(function (data, status, headers, config) {
                                $scope.failed = true;
                            });
                        }
                        
                    }
                }
            }).
            when('/submit', {
                templateUrl: 'submit.html',
                controller: function ($scope, $location, $http, httpHelper) {

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

                                httpHelper.postMultiPartRequest($scope, $scope.$parent.postUrl,
                                [
                                    {
                                        key: "model",
                                        value: {
                                            articulateNodeId: $scope.$parent.articulateNodeId,
                                            title: $scope.$parent.title,
                                            body: $scope.$parent.md,
                                            tags: $scope.$parent.tags,
                                            categories: $scope.$parent.categories,
                                            excerpt: $scope.$parent.excerpt,
                                            slug: $scope.$parent.slug
                                        }
                                    }
                                ], function (d, formData) {
                                    //now add all of the assigned files
                                    for (var f in $scope.$parent.files) {                                        
                                        formData.append($scope.$parent.files[f].name, $scope.$parent.files[f]);
                                    }
                                },
                                   function (d, status, headers, config) {
                                       $scope.result = d;
                                       $scope.$parent.caption = "Post successful";
                                }, function (d, status, headers, config) {
                                    if (d.Message) {
                                        alert(d.Message);
                                    }
                                    else {
                                        alert("Failed! " + angular.toJson(d));
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

    $scope.files = [];
    $scope.nextPath = null;
    $scope.prevPath = null;
    $scope.nextText = "redo";
    $scope.prevText = "undo";
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
            $scope.articulateForm.$setPristine();
        }
    }

});

articulateapp.directive('filesSelected', function () {
    return {
        restrict: "A",
        scope: true, //create a new scope
        link: function(scope, el, attrs) {
            el.bind('change', function(event) {
                var files = event.target.files;
                //emit event upward
                scope.$emit("filesSelected", { files: files });
            });
        }
    };
});

//used to refresh the material design objects when the template changes
articulateapp.directive('materialRefresh', function ($timeout) {
    return {
        restrict: "E",
        link: function (scope, el, attrs) {
            $timeout(function () {
                componentHandler.upgradeAllRegistered();
            });
        }
    };
});

articulateapp.factory("angularHelper", function() {
    return {
        safeApply: function (scope, fn) {
            if (scope.$$phase || scope.$root.$$phase) {
                if (angular.isFunction(fn)) {
                    fn();
                }
            }
            else {
                if (angular.isFunction(fn)) {
                    scope.$apply(fn);
                }
                else {
                    scope.$apply();
                }
            }
        }
    }
});

articulateapp.factory("httpHelper", function ($http, angularHelper) {
    return {
        postMultiPartRequest: function(scope, url, jsonData, transformCallback, successCallback, failureCallback) {

            //validate input, jsonData can be an array of key/value pairs or just one key/value pair.
            if (!jsonData) {
                throw "jsonData cannot be null";
            }

            if (angular.isArray(jsonData)) {
                angular.forEach(jsonData, function(item) {
                    if (!item.key || !item.value) {
                        throw "jsonData array item must have both a key and a value property";
                    }
                });
            }
            else if (!jsonData.key || !jsonData.value) {
                throw "jsonData object must have both a key and a value property";
            }


            angularHelper.safeApply(scope, function() {
                $http({
                    method: 'POST',
                    url: url,
                    //IMPORTANT!!! You might think this should be set to 'multipart/form-data' but this is not true because when we are sending up files
                    // the request needs to include a 'boundary' parameter which identifies the boundary name between parts in this multi-part request
                    // and setting the Content-type manually will not set this boundary parameter. For whatever reason, setting the Content-type to 'false'
                    // will force the request to automatically populate the headers properly including the boundary parameter.
                    headers: { 'Content-Type': undefined },
                    transformRequest: function (data) {
                        var formData = new FormData();
                        //add the json data
                        if (angular.isArray(data)) {
                            angular.forEach(data, function (item) {
                                formData.append(item.key, !angular.isString(item.value) ? angular.toJson(item.value) : item.value);
                            });
                        }
                        else {
                            formData.append(data.key, !angular.isString(data.value) ? angular.toJson(data.value) : data.value);
                        }

                        //call the callback
                        if (transformCallback) {
                            transformCallback.apply(this, [data, formData]);
                        }

                        return formData;
                    },
                    data: jsonData
                }).
                success(function (data, status, headers, config) {
                    if (successCallback) {
                        successCallback.apply(this, [data, status, headers, config]);
                    }
                }).
                error(function (data, status, headers, config) {
                    if (failureCallback) {
                        failureCallback.apply(this, [data, status, headers, config]);
                    }
                });
            });
            
        }
    }
});


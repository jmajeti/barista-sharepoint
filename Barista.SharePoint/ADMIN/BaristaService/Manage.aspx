﻿<%@ Assembly Name="$SharePoint.Project.AssemblyFullName$" %>
<%@ Import Namespace="Microsoft.SharePoint.ApplicationPages" %>
<%@ Register Tagprefix="SharePoint" Namespace="Microsoft.SharePoint.WebControls" Assembly="Microsoft.SharePoint, Version=14.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c" %>
<%@ Register Tagprefix="Utilities" Namespace="Microsoft.SharePoint.Utilities" Assembly="Microsoft.SharePoint, Version=14.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c" %>
<%@ Register Tagprefix="asp" Namespace="System.Web.UI" Assembly="System.Web.Extensions, Version=3.5.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" %>
<%@ Import Namespace="Microsoft.SharePoint" %>
<%@ Assembly Name="Microsoft.Web.CommandUI, Version=14.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c" %>
<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Manage.aspx.cs" Inherits="Barista.SharePoint.Manage" DynamicMasterPageFile="~masterurl/default.master" %>

<asp:Content ID="PageTitle" ContentPlaceHolderID="PlaceHolderPageTitle" runat="server">
    Barista Service Application
</asp:Content>

<asp:Content ID="PageTitleInTitleArea" ContentPlaceHolderID="PlaceHolderPageTitleInTitleArea" runat="server" >
    Manage Barista Service Application
</asp:Content>

<asp:Content ID="PlaceHolderAdditionalPageHead" ContentPlaceHolderID="PlaceHolderAdditionalPageHead" runat="server">
    <SharePoint:CssLink Id="BootstrapCssLink" runat="server" DefaultUrl="/_layouts/BaristaJS/bootstrap-2.3.1/css/bootstrap.min.css"></SharePoint:CssLink>
    <SharePoint:CssLink Id="AngularUiCssLink" runat="server" DefaultUrl="/_layouts/BaristaJS/angular/angular-ui-0.4.0.min.css"></SharePoint:CssLink>
    <SharePoint:CssLink Id="KendoCommonCssLink" runat="server" DefaultUrl="/_layouts/BaristaJS/kendoui.complete.2013.1.319/styles/kendo.common.min.css"></SharePoint:CssLink>
    <SharePoint:CssLink Id="KendoDefaultCssLink" runat="server" DefaultUrl="/_layouts/BaristaJS/kendoui.complete.2013.1.319/styles/kendo.default.min.css"></SharePoint:CssLink>
    <style type="text/css">
        a:visited {
            color: black;
        }

        div.k-window-content {
            position: static;
        }

        div.k-window {
            display: table;
        }

        /* Override Bootstrap styles that conflict with corev4.css*/
        img {
            max-width: none;
        }
    </style>
</asp:Content>

<asp:Content ID="Main" ContentPlaceHolderID="PlaceHolderMain" runat="server">
    <div class="container-fluid" style="padding-top:5px;" ng-app="managebarista">
        <ul class="nav nav-tabs" style="height: 37px" ng-controller="ManageMainCtrl">
            <li ui-route="{{tab.route}}" ng-repeat="tab in tabs" ng-class="{active: $uiRoute}" class="ng-scope ng-binding" style="cursor:pointer">
                <a ng-click="reload($event, tab.route)">{{tab.title}}</a>
            </li>
        </ul>
        <div id="main-content">
            <div ng-view></div>
        </div>
    </div>
    
    <script type="text/javascript" src="/_layouts/BaristaJS/json2.js"></script>
    <script type="text/javascript" src="/_layouts/BaristaJS/underscore-1.4.4.min.js"></script>
    <script type="text/javascript" src="/_layouts/BaristaJS/modernizr-2.6.2.js"></script>
    <script type="text/javascript" src="/_layouts/BaristaJS/jquery-1.9.1.min.js"></script>
    <script type="text/javascript" src="/_layouts/BaristaJS/jquery.validate-1.10.0.min.js"></script>
    <script type="text/javascript" src="/_layouts/BaristaJS/jquery.validate.additional-methods-1.10.0.min.js"></script>
    <script type="text/javascript" src="/_layouts/BaristaJS/jquery-ui-1.10.0/js/jquery-ui-1.10.0.custom.min.js"></script>
    <script type="text/javascript" src="/_layouts/BaristaJS/angular/angular.min.js"></script>
    <script type="text/javascript" src="/_layouts/BaristaJS/angular/angular-ui-0.4.0.min.js"></script>
    <script type="text/javascript" src="/_layouts/BaristaJS/angular/angular-ui-ieshiv.min.js"></script>
    
    <script type="text/javascript" src="/_layouts/BaristaJS/bootstrap-2.3.1/js/bootstrap.min.js"></script>
    <script type="text/javascript" src="/_layouts/BaristaJS/angular/angular-ui-bootstrap-0.2.0.min.js"></script>
<%--    <script type="text/javascript" src="/_layouts/BaristaJS/angular/angular-ui-bootstrap-0.2.0.tpls.min.js"></script>--%>
    <script type="text/javascript" src="/_layouts/BaristaJS/kendoui.complete.2013.1.319/js/kendo.web.min.js"></script>
    <script type="text/javascript" src="/_layouts/BaristaJS/angular/angular-kendo.min.js"></script>
    
    <script type="text/javascript" src="/_admin/BaristaService/Scripts/app.js"></script>
    <script type="text/javascript" src="/_admin/BaristaService/Scripts/Controllers/ManageIndexesCtrl.js"></script>
</asp:Content>
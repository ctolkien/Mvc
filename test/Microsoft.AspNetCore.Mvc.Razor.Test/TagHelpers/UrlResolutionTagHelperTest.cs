// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.TestCommon;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.WebEncoders.Testing;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Razor.TagHelpers
{
    public class UrlResolutionTagHelperTest
    {
        public static TheoryData ResolvableUrlData
        {
            get
            {
                // url, expectedHref
                return new TheoryData<string, string>
                {
                   { "~/home/index.html", "/approot/home/index.html" },
                   { "  ~/home/index.html", "/approot/home/index.html" },
                   {
                        "~/home/index.html ~/secondValue/index.html",
                        "/approot/home/index.html ~/secondValue/index.html"
                   },
                };
            }
        }

        [Fact]
        public void Process_DoesNothingIfTagNameIsNull()
        {
            // Arrange
            var tagHelperOutput = new TagHelperOutput(
                tagName: null,
                attributes: new TagHelperAttributeList
                {
                    { "href", "~/home/index.html" }
                },
                getChildContentAsync: (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(null));

            var tagHelper = new UrlResolutionTagHelper(Mock.Of<IUrlHelperFactory>(), new HtmlTestEncoder());
            var context = new TagHelperContext(
                allAttributes: new TagHelperAttributeList(
                    Enumerable.Empty<TagHelperAttribute>()),
                items: new Dictionary<object, object>(),
                uniqueId: "test");

            // Act
            tagHelper.Process(context, tagHelperOutput);

            // Assert
            var attribute = Assert.Single(tagHelperOutput.Attributes);
            Assert.Equal("href", attribute.Name, StringComparer.Ordinal);
            var attributeValue = Assert.IsType<string>(attribute.Value);
            Assert.Equal("~/home/index.html", attributeValue, StringComparer.Ordinal);
        }

        [Theory]
        [MemberData(nameof(ResolvableUrlData))]
        public void Process_ResolvesTildeSlashValues(string url, string expectedHref)
        {
            // Arrange
            var tagHelperOutput = new TagHelperOutput(
                tagName: "a",
                attributes: new TagHelperAttributeList
                {
                    { "href", url }
                },
                getChildContentAsync: (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(null));
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock
                .Setup(urlHelper => urlHelper.Content(It.IsAny<string>()))
                .Returns(new Func<string, string>(value => "/approot" + value.Substring(1)));
            var urlHelperFactory = new Mock<IUrlHelperFactory>();
            urlHelperFactory
                .Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>()))
                .Returns(urlHelperMock.Object);
            var tagHelper = new UrlResolutionTagHelper(urlHelperFactory.Object, new HtmlTestEncoder());

            var context = new TagHelperContext(
                allAttributes: new TagHelperAttributeList(
                    Enumerable.Empty<TagHelperAttribute>()),
                items: new Dictionary<object, object>(),
                uniqueId: "test");

            // Act
            tagHelper.Process(context, tagHelperOutput);

            // Assert
            var attribute = Assert.Single(tagHelperOutput.Attributes);
            Assert.Equal("href", attribute.Name, StringComparer.Ordinal);
            var attributeValue = Assert.IsType<string>(attribute.Value);
            Assert.Equal(expectedHref, attributeValue, StringComparer.Ordinal);
            Assert.Equal(HtmlAttributeValueStyle.DoubleQuotes, attribute.ValueStyle);
        }

        public static TheoryData ResolvableUrlHtmlStringData
        {
            get
            {
                // url, expectedHref
                return new TheoryData<HtmlString, string>
                {
                   { new HtmlString("~/home/index.html"), "HtmlEncode[[/approot/]]home/index.html" },
                   { new HtmlString("  ~/home/index.html"), "HtmlEncode[[/approot/]]home/index.html" },
                   {
                        new HtmlString("~/home/index.html ~/secondValue/index.html"),
                        "HtmlEncode[[/approot/]]home/index.html ~/secondValue/index.html"
                   },
                };
            }
        }

        [Theory]
        [MemberData(nameof(ResolvableUrlHtmlStringData))]
        public void Process_ResolvesTildeSlashValues_InHtmlString(object url, string expectedHref)
        {
            // Arrange
            var tagHelperOutput = new TagHelperOutput(
                tagName: "a",
                attributes: new TagHelperAttributeList
                {
                    { "href", url }
                },
                getChildContentAsync: (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(null));
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock
                .Setup(urlHelper => urlHelper.Content(It.IsAny<string>()))
                .Returns(new Func<string, string>(value => "/approot" + value.Substring(1)));
            var urlHelperFactory = new Mock<IUrlHelperFactory>();
            urlHelperFactory
                .Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>()))
                .Returns(urlHelperMock.Object);
            var tagHelper = new UrlResolutionTagHelper(urlHelperFactory.Object, new HtmlTestEncoder());

            var context = new TagHelperContext(
                allAttributes: new TagHelperAttributeList(
                    Enumerable.Empty<TagHelperAttribute>()),
                items: new Dictionary<object, object>(),
                uniqueId: "test");

            // Act
            tagHelper.Process(context, tagHelperOutput);

            // Assert
            var attribute = Assert.Single(tagHelperOutput.Attributes);
            Assert.Equal("href", attribute.Name, StringComparer.Ordinal);
            var htmlContent = Assert.IsAssignableFrom<IHtmlContent>(attribute.Value);
            Assert.Equal(expectedHref, HtmlContentUtilities.HtmlContentToString(htmlContent), StringComparer.Ordinal);
            Assert.Equal(HtmlAttributeValueStyle.DoubleQuotes, attribute.ValueStyle);
        }

        public static TheoryData UnresolvableUrlData
        {
            get
            {
                // url
                return new TheoryData<string>
                {
                   { "/home/index.html" },
                   { "~ /home/index.html" },
                   { "/home/index.html ~/second/wontresolve.html" },
                   { "  ~\\home\\index.html" },
                   { "~\\/home/index.html" },
                };
            }
        }

        public static TheoryData ResolvableSrcSetAttributeData
        {
            get
            {
                // url, expectedHref
                return new TheoryData<string, string>
                 {
                    { "~/content/image.png 1x", "/approot/content/image.png 1x" },
                    { "  ~/content/image.png 1x, ~/content/image@2.png 2x", "HtmlEncode[[/approot/]]/content/image.png 1x, HtmlEncode[[/approot/]]/content/image@2.png 2x" },
                    { "~/content/image.png ~/secondValue/image.png", "HtmlEncode[[/approot/]]/content/image.png ~/secondValue/image.png" },
                    { "~/content/image.png 200w, ~/content/image@2.png 400w", "HtmlEncode[[/approot/]]/content/image.png 200w, HtmlEncode[[/approot/]]/content/image@2.png 400w" },
                    { "/content/image.png 200w, ~/content/image@2.png 400w", "/content/image.png 200w, HtmlEncode[[/approot/]]/content/image@2.png 400w" },

                 };
            }
        }

        [Theory]
        [MemberData(nameof(ResolvableSrcSetAttributeData))]
        public void Process_ResolvesImgSrcSetWithMultipleUrls(string url, string expectedHref)
        {
            // Arrange
            var tagHelperOutput = new TagHelperOutput(tagName: "img",
                attributes: new TagHelperAttributeList
                {
                     { "srcset", url }
                },
                getChildContentAsync: (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(null));

            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock
                .Setup(urlHelper => urlHelper.Content(It.IsAny<string>()))
                .Returns(new Func<string, string>(value => "/approot" + value.Substring(1)));

            var urlHelperFactory = new Mock<IUrlHelperFactory>();
            urlHelperFactory
                .Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>()))
                .Returns(urlHelperMock.Object);

            var tagHelper = new UrlResolutionTagHelper(urlHelperFactory.Object, new HtmlTestEncoder());

            var context = new TagHelperContext(
                allAttributes: new TagHelperAttributeList(
                    Enumerable.Empty<TagHelperAttribute>()),
                items: new Dictionary<object, object>(),
                uniqueId: "test");

            // Act
            tagHelper.Process(context, tagHelperOutput);

            // Assert
            var attribute = Assert.Single(tagHelperOutput.Attributes);
            Assert.Equal("srcset", attribute.Name, StringComparer.Ordinal);
            var attributeValue = Assert.IsType<string>(attribute.Value);
            Assert.Equal(expectedHref, attributeValue, StringComparer.Ordinal);
            Assert.Equal(HtmlAttributeValueStyle.DoubleQuotes, attribute.ValueStyle);

        }

        public static TheoryData ResolvableSrcSetHtmlStringData
        {
            get
            {
                // url, expectedHref
                return new TheoryData<HtmlString, string>
                 {
                    { new HtmlString("~/content/image.png 1x"), "/approot/content/image.png 1x" },
                    { new HtmlString("  ~/content/image.png 1x, ~/content/image@2.png 2x"), "/approot/content/image.png 1x, /approot/content/image@2.png 2x" },
                    { new HtmlString("~/content/image.png ~/secondValue/image.png"), "/approot/content/image.png ~/secondValue/image.png" },
                    { new HtmlString("~/content/image.png 200w, ~/content/image@2.png 400w"), "/approot/content/image.png 200w, /approot/content/image@2.png 400w" },
                    { new HtmlString("/content/image.png 200w, ~/content/image@2.png 400w"), "/content/image.png 200w, /approot/content/image@2.png 400w" },
                 };
            }
        }

        [Theory]
        [MemberData(nameof(ResolvableSrcSetHtmlStringData))]
        public void Process_ResolvesImgSrcSetWithMultipleUrls_InHtmlString(HtmlString url, string expectedHref)
        {
            // Arrange
            var tagHelperOutput = new TagHelperOutput(tagName: "img",
                attributes: new TagHelperAttributeList
                {
                     { "srcset", url }
                },
                getChildContentAsync: (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(null));

            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock
                .Setup(urlHelper => urlHelper.Content(It.IsAny<string>()))
                .Returns(new Func<string, string>(value => "/approot" + value.Substring(1)));

            var urlHelperFactory = new Mock<IUrlHelperFactory>();
            urlHelperFactory
                .Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>()))
                .Returns(urlHelperMock.Object);

            var tagHelper = new UrlResolutionTagHelper(urlHelperFactory.Object, new HtmlTestEncoder());

            var context = new TagHelperContext(
                allAttributes: new TagHelperAttributeList(
                    Enumerable.Empty<TagHelperAttribute>()),
                items: new Dictionary<object, object>(),
                uniqueId: "test");

            // Act
            tagHelper.Process(context, tagHelperOutput);

            // Assert
            var attribute = Assert.Single(tagHelperOutput.Attributes);
            Assert.Equal("srcset", attribute.Name, StringComparer.Ordinal);
            var htmlContent = Assert.IsAssignableFrom<IHtmlContent>(attribute.Value);
            Assert.Equal(expectedHref, HtmlContentUtilities.HtmlContentToString(htmlContent), StringComparer.Ordinal);
            Assert.Equal(HtmlAttributeValueStyle.DoubleQuotes, attribute.ValueStyle);

        }

        [Theory]
        [MemberData(nameof(UnresolvableUrlData))]
        public void Process_DoesNotResolveNonTildeSlashValues(string url)
        {
            // Arrange
            var tagHelperOutput = new TagHelperOutput(
                tagName: "a",
                attributes: new TagHelperAttributeList
                {
                    { "href", url }
                },
                getChildContentAsync: (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(null));
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock
                .Setup(urlHelper => urlHelper.Content(It.IsAny<string>()))
                .Returns("approot/home/index.html");
            var urlHelperFactory = new Mock<IUrlHelperFactory>();
            urlHelperFactory
                .Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>()))
                .Returns(urlHelperMock.Object);
            var tagHelper = new UrlResolutionTagHelper(urlHelperFactory.Object, new HtmlTestEncoder());

            var context = new TagHelperContext(
                allAttributes: new TagHelperAttributeList(
                    Enumerable.Empty<TagHelperAttribute>()),
                items: new Dictionary<object, object>(),
                uniqueId: "test");

            // Act
            tagHelper.Process(context, tagHelperOutput);

            // Assert
            var attribute = Assert.Single(tagHelperOutput.Attributes);
            Assert.Equal("href", attribute.Name, StringComparer.Ordinal);
            var attributeValue = Assert.IsType<string>(attribute.Value);
            Assert.Equal(url, attributeValue, StringComparer.Ordinal);
            Assert.Equal(HtmlAttributeValueStyle.DoubleQuotes, attribute.ValueStyle);
        }

        public static TheoryData UnresolvableUrlHtmlStringData
        {
            get
            {
                // url
                return new TheoryData<HtmlString>
                {
                   { new HtmlString("/home/index.html") },
                   { new HtmlString("~ /home/index.html") },
                   { new HtmlString("/home/index.html ~/second/wontresolve.html") },
                   { new HtmlString("~\\home\\index.html") },
                   { new HtmlString("~\\/home/index.html") },
                };
            }
        }

        [Theory]
        [MemberData(nameof(UnresolvableUrlHtmlStringData))]
        public void Process_DoesNotResolveNonTildeSlashValues_InHtmlString(HtmlString url)
        {
            // Arrange
            var tagHelperOutput = new TagHelperOutput(
                tagName: "a",
                attributes: new TagHelperAttributeList
                {
                    { "href", url }
                },
                getChildContentAsync: (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(null));
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock
                .Setup(urlHelper => urlHelper.Content(It.IsAny<string>()))
                .Returns("approot/home/index.html");
            var urlHelperFactory = new Mock<IUrlHelperFactory>();
            urlHelperFactory
                .Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>()))
                .Returns(urlHelperMock.Object);
            var tagHelper = new UrlResolutionTagHelper(urlHelperFactory.Object, new HtmlTestEncoder());

            var context = new TagHelperContext(
                allAttributes: new TagHelperAttributeList(
                    Enumerable.Empty<TagHelperAttribute>()),
                items: new Dictionary<object, object>(),
                uniqueId: "test");

            // Act
            tagHelper.Process(context, tagHelperOutput);

            // Assert
            var attribute = Assert.Single(tagHelperOutput.Attributes);
            Assert.Equal("href", attribute.Name, StringComparer.Ordinal);
            var attributeValue = Assert.IsType<HtmlString>(attribute.Value);
            Assert.Equal(url.ToString(), attributeValue.ToString(), StringComparer.Ordinal);
            Assert.Equal(HtmlAttributeValueStyle.DoubleQuotes, attribute.ValueStyle);
        }

        [Fact]
        public void Process_IgnoresNonHtmlStringOrStringValues()
        {
            // Arrange
            var tagHelperOutput = new TagHelperOutput(
                tagName: "a",
                attributes: new TagHelperAttributeList
                {
                    { "href", true }
                },
                getChildContentAsync: (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(null));
            var tagHelper = new UrlResolutionTagHelper(urlHelperFactory: null, htmlEncoder: null);

            var context = new TagHelperContext(
                allAttributes: new TagHelperAttributeList(
                    Enumerable.Empty<TagHelperAttribute>()),
                items: new Dictionary<object, object>(),
                uniqueId: "test");

            // Act
            tagHelper.Process(context, tagHelperOutput);

            // Assert
            var attribute = Assert.Single(tagHelperOutput.Attributes);
            Assert.Equal("href", attribute.Name, StringComparer.Ordinal);
            Assert.Equal(true, attribute.Value);
            Assert.Equal(HtmlAttributeValueStyle.DoubleQuotes, attribute.ValueStyle);
        }

        [Fact]
        public void Process_ThrowsWhenEncodingNeededAndIUrlHelperActsUnexpectedly()
        {
            // Arrange
            var relativeUrl = "~/home/index.html";
            var expectedExceptionMessage = Resources.FormatCouldNotResolveApplicationRelativeUrl_TagHelper(
                relativeUrl,
                nameof(IUrlHelper),
                nameof(IUrlHelper.Content),
                "removeTagHelper",
                typeof(UrlResolutionTagHelper).FullName,
                typeof(UrlResolutionTagHelper).GetTypeInfo().Assembly.GetName().Name);
            var tagHelperOutput = new TagHelperOutput(
                tagName: "a",
                attributes: new TagHelperAttributeList
                {
                    { "href", new HtmlString(relativeUrl) }
                },
                getChildContentAsync: (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(null));
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock
                .Setup(urlHelper => urlHelper.Content(It.IsAny<string>()))
                .Returns("UnexpectedResult");
            var urlHelperFactory = new Mock<IUrlHelperFactory>();
            urlHelperFactory
                .Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>()))
                .Returns(urlHelperMock.Object);
            var tagHelper = new UrlResolutionTagHelper(urlHelperFactory.Object, new HtmlTestEncoder());

            var context = new TagHelperContext(
                allAttributes: new TagHelperAttributeList(
                    Enumerable.Empty<TagHelperAttribute>()),
                items: new Dictionary<object, object>(),
                uniqueId: "test");

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(
                () => tagHelper.Process(context, tagHelperOutput));
            Assert.Equal(expectedExceptionMessage, exception.Message, StringComparer.Ordinal);
        }
    }
}

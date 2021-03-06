﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AutoBogus;
using FluentAssertions;
using Golap.AppleAuth.Entities;
using Golap.AppleAuth.Exceptions;
using Golap.AppleAuth.Tests.Core;
using Moq;
using Xunit;

namespace Golap.AppleAuth.Tests
{
    public class AppleAuthClientTest
    {
        #region data

        public class ConstructorBadArgsData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                var a = new AppleAuthSetting("a", "b", "https://apple.com");
                var b = new AppleTokenGenerator("a", "b", new AppleKeySetting("a", "b"));
                var c = new HttpClient();
                yield return new object[] { a, b, null };
                yield return new object[] { a, null, c };
                yield return new object[] { null, b, c };
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        #endregion

        private readonly Mock<IAppleTokenGenerator> _appleTokenGeneratorMock;

        public AppleAuthClientTest()
        {
            _appleTokenGeneratorMock = new Mock<IAppleTokenGenerator>();
        }

        [Theory]
        [ClassData(typeof(ConstructorBadArgsData))]
        public void AppleAuthClient_NotValidArgument_ThrowArgumentException(AppleAuthSetting authSetting, IAppleTokenGenerator privateKeySetting, HttpClient httpClient)
        {
            FluentActions.Invoking(() => new AppleAuthClient(authSetting, privateKeySetting, httpClient)).Should().Throw<ArgumentException>();
        }

        [Fact]
        public void LoginUri_CreateWithRightQuery()
        {
            var handlerStub = new DelegatingHandlerStub(null);
            var settings = new AppleAuthSetting("a", "b", "https://apple.com", "x y z");
            var client = GetClient(settings, handlerStub);

            var result = client.LoginUri();
            var query = System.Web.HttpUtility.ParseQueryString(result.Query);

            query.AllKeys.Should().BeEquivalentTo("response_type", "client_id", "redirect_uri", "state", "scope", "response_mode");
            query.Get("response_type").Should().Be("code id_token");
            query.Get("client_id").Should().Be("b");
            query.Get("redirect_uri").Should().Be("https://apple.com");
            query.Get("scope").Should().Be("x y z");
            query.Get("response_mode").Should().Be("form_post");
            result.ToString().Should().Contain("redirect_uri=https%3a%2f%2fapple.com");
        }

        [Fact]
        public void LoginUri_CreateAlwaysANewState()
        {
            var handlerStub = new DelegatingHandlerStub(null);
            var settings = new AppleAuthSetting("a", "b", "https://apple.com", "x y z");
            var client = GetClient(settings, handlerStub);

            var result1 =  client.LoginUri();
            var result2 = client.LoginUri();

            var query1 = System.Web.HttpUtility.ParseQueryString(result1.Query);
            var query2 = System.Web.HttpUtility.ParseQueryString(result2.Query);

            query1.Get("state").Should().NotBe(query2.Get("state"));
        }

        [Fact]
        public async Task AccessTokenAsync_ReturnAccessToken()
        {
            _appleTokenGeneratorMock.Setup(e => e.Generate(It.IsAny<TimeSpan>())).Returns("abc");
            var response = AutoFaker.Generate<AppleAccessToken>();
            var handlerStub = new DelegatingHandlerStub(new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(response, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })),
            });
            var client = GetClient(AutoFaker.Generate<AppleAuthSetting>(), handlerStub);

            var result = await client.AccessTokenAsync("abc");

            result.Should().BeEquivalentTo(response);
        }

        [Fact]
        public async Task AccessTokenAsync_AppleReturnError_ThrowAppleAuthException()
        {
            var responseData = "error";
            var handlerStub = new DelegatingHandlerStub(new HttpResponseMessage() { StatusCode = HttpStatusCode.BadRequest, Content = new StringContent(responseData) });
            var client = GetClient(AutoFaker.Generate<AppleAuthSetting>(), handlerStub);

            await FluentActions.Invoking(() => client.AccessTokenAsync("abc")).Should().ThrowAsync<AppleAuthException>().WithMessage(responseData);
        }

        [Fact]
        public async Task RefreshTokenAsync_ReturnAccessToken()
        {
            _appleTokenGeneratorMock.Setup(e => e.Generate(It.IsAny<TimeSpan>())).Returns("abc");
            var response = AutoFaker.Generate<AppleAccessToken>();
            var handlerStub = new DelegatingHandlerStub(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(response, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })),
                });
            var client = GetClient(AutoFaker.Generate<AppleAuthSetting>(), handlerStub);

            var result = await client.RefreshTokenAsync("abc");

            result.Should().BeEquivalentTo(response);
        }

        [Fact]
        public async Task RefreshTokenAsync_AppleReturnError_ThrowAppleAuthException()
        {
            var responseData = "error";
            var handlerStub = new DelegatingHandlerStub(new HttpResponseMessage() { StatusCode = HttpStatusCode.BadRequest, Content = new StringContent(responseData) });
            var client = GetClient(AutoFaker.Generate<AppleAuthSetting>(), handlerStub);

            await FluentActions.Invoking(() => client.RefreshTokenAsync("abc")).Should().ThrowAsync<AppleAuthException>().WithMessage(responseData);
        }

        private AppleAuthClient GetClient(AppleAuthSetting appleAuthSetting, DelegatingHandler handler)
        {
            return new AppleAuthClient(appleAuthSetting, _appleTokenGeneratorMock.Object, new HttpClient(handler));
        }
    }
}

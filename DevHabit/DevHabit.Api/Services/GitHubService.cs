﻿using System.Net.Http.Headers;
using DevHabit.Api.DTOs.GitHub;
using Newtonsoft.Json;

namespace DevHabit.Api.Services;

public sealed class GitHubService(IHttpClientFactory httpClientFactory, ILogger<GitHubService> logger)
{
    public async Task<GitHubUserProfileDto?> GetUserProfileAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        using HttpClient client = CreateGitHubClient(accessToken);

        HttpResponseMessage response = await client.GetAsync("user", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to get user profile from GitHub. Status code: {StatusCode}", response.StatusCode);
            return null;
        }

        string content = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonConvert.DeserializeObject<GitHubUserProfileDto>(content);
    }

    public async Task<IReadOnlyList<GitHubEventDto>?> GetUserEventsAsync(
        string username,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);

        using HttpClient client = CreateGitHubClient(accessToken);

        HttpResponseMessage response = await client.GetAsync(
            $"users/{username}/events?per_page=100",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Failed to get user events from GitHub. Status code: {StatusCode}", response.StatusCode);
            return null;
        }

        string content = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonConvert.DeserializeObject<IReadOnlyList<GitHubEventDto>>(content);
    }

    private HttpClient CreateGitHubClient(string accessToken)
    {
        HttpClient client = httpClientFactory.CreateClient("github");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }
}

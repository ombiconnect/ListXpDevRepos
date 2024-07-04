using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string xpDevBaseUrl = "https://iconnect.xp-dev.com/api/v1"; // XPDev API base URL
        string xpDevToken = "8e0da004f615b684d935242dc8d448ee"; // XPDev token
        string azureDevOpsOrganization = "iconnectsolutions"; // Replace with your Azure DevOps organization
        string azureDevOpsProject = "Repos"; // Replace with your Azure DevOps project name
        string azureDevOpsToken = "jz74m2hym3x3oegf7zrkzc4hvabxa3z5sfqx5n6oul5dwhvwjd4q"; // Replace with your Azure DevOps PAT

        await ListProjects(xpDevBaseUrl, xpDevToken, azureDevOpsOrganization, azureDevOpsProject, azureDevOpsToken);
    }

    static async Task ListProjects(string xpDevBaseUrl, string xpDevToken, string azureDevOpsOrganization, string azureDevOpsProject, string azureDevOpsToken)
    {
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("X-XPDevToken", xpDevToken);

            string projectsUrl = $"{xpDevBaseUrl}/projects";
            HttpResponseMessage response = await client.GetAsync(projectsUrl);

            if (response.IsSuccessStatusCode)
            {
                string projectsJson = await response.Content.ReadAsStringAsync();
                dynamic projects = Newtonsoft.Json.JsonConvert.DeserializeObject(projectsJson);
                List<dynamic> projectList = ((IEnumerable<dynamic>)projects).ToList();

                // Reverse the collection of projects
                projectList.Reverse();
                foreach (var project in projectList)
                {
                    string projectId = project.id;
                    string projectName = project.name;
                    await ListRepositories(xpDevBaseUrl, xpDevToken, azureDevOpsOrganization, azureDevOpsProject, projectName, projectId, azureDevOpsToken);
                }
            }
        }
    }

    static async Task ListRepositories(string xpDevBaseUrl, string xpDevToken, string azureDevOpsOrganization, string azureDevOpsProject, string projectName, string projectId, string azureDevOpsToken)
    {
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("X-XPDevToken", xpDevToken);

            string repositoriesUrl = $"{xpDevBaseUrl}/repositories/project/{projectId}";
            HttpResponseMessage response = await client.GetAsync(repositoriesUrl);

            if (response.IsSuccessStatusCode)
            {
                string repositoriesJson = await response.Content.ReadAsStringAsync();
                dynamic repositories = Newtonsoft.Json.JsonConvert.DeserializeObject(repositoriesJson);

                foreach (var repository in repositories)
                {
                    string repoName = repository.name;
                    string repoType = repository.type;

                    if (repoType.Equals("Git", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"ProjectName = {projectName}");

                        if (repoName == "Native_Frontend")
                        {
                            Console.WriteLine("_________________________________________________________________________________________________________");

                            Console.WriteLine($"RepoName = {repoName}");

                            string cloneDir = Path.Combine("D:\\XpDevRepositories", projectName, repoName);

                            string isClone = await CloneGitRepository(repoName, cloneDir, $"https://iconnect.xp-dev.com/git/{repoName}");
                            Console.WriteLine($"IsClone = {isClone}");

            
                                string isFetched = await FetchFromAzureDevOps(cloneDir, "origin", new[] { "+refs/heads/*:refs/remotes/origin/*" }, azureDevOpsToken, "Fetching all branches");
                                Console.WriteLine($"IsFetched = {isFetched}");

                                string isPulled = await PullFromAzureDevOps(cloneDir, azureDevOpsToken);
                                Console.WriteLine($"IsPulled = {isPulled}");

                                string isPushed = await PushToAzureDevOps(cloneDir, azureDevOpsOrganization, azureDevOpsProject, repoName, azureDevOpsToken);
                                Console.WriteLine($"IsPushed = {isPushed}");
                            

                            Console.WriteLine("_________________________________________________________________________________________________________");
                        }
                    }
                }
            }
        }
    }

    static async Task<string> CloneGitRepository(string repoName, string cloneDir, string remoteUrl)
    {
        try
        {
            // Check if the directory already exists and is not empty
            if (Directory.Exists(cloneDir) && Directory.EnumerateFileSystemEntries(cloneDir).Any())
            {
                return $"false: Directory '{cloneDir}' already exists and is not empty";
            }

            // Clean up the directory if it exists
            if (Directory.Exists(cloneDir))
            {
                Directory.Delete(cloneDir, true);
            }

            // Create the clone directory
            Directory.CreateDirectory(cloneDir);

            // Clone the repository
            Repository.Clone(remoteUrl, cloneDir);

            return "true";
        }
        catch (Exception ex)
        {
            return $"false: {ex.Message}";
        }
    }

    static async Task<string> FetchFromAzureDevOps(string cloneDir, string remote, IEnumerable<string> refspecs, string azureDevOpsToken, string logMessage)
    {
        try
        {
            using (var repo = new Repository(cloneDir))
            {
                var fetchOptions = new FetchOptions
                {
                    CredentialsProvider = (url, usernameFromUrl, types) =>
                        new UsernamePasswordCredentials
                        {
                            Username = "iconnectsolutions",
                            Password = azureDevOpsToken
                        }
                };

                Commands.Fetch(repo, remote, refspecs, fetchOptions, logMessage);
            }

            return "true";
        }
        catch (Exception ex)
        {
            return $"false: {ex.Message}";
        }
    }

    static async Task<string> PullFromAzureDevOps(string cloneDir, string azureDevOpsToken)
    {
        try
        {
            using (var repo = new Repository(cloneDir))
            {
                // Set up the pull options
                var pullOptions = new PullOptions
                {
                    FetchOptions = new FetchOptions
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) =>
                            new UsernamePasswordCredentials
                            {
                                Username = "iconnectsolutions",
                                Password = azureDevOpsToken
                            }
                    },
                    MergeOptions = new MergeOptions()
                };

                // Pull changes from the remote
                MergeResult mergeResult = Commands.Pull(repo, new Signature("Your Name", "your.email@example.com", DateTimeOffset.Now), pullOptions);

                if (mergeResult.Status == MergeStatus.UpToDate)
                {
                    return "true";
                }
                else
                {
                    return $"false: Pull resulted in a non-up-to-date status ({mergeResult.Status})";
                }
            }
        }
        catch (Exception ex)
        {
            return $"false: {ex.Message}";
        }
    }

    static async Task<string> PushToAzureDevOps(string cloneDir, string organization, string project, string repoName, string azureDevOpsToken)
    {
        try
        {
            using (var repo = new Repository(cloneDir))
            {
                // Set remote URL for Azure DevOps repository
                string remoteUrl = $"https://{organization}@dev.azure.com/{organization}/{project}/_git/{repoName}";
                var remote = repo.Network.Remotes["origin"];
                var pushOptions = new PushOptions
                {
                    CredentialsProvider = (url, usernameFromUrl, types) =>
                        new UsernamePasswordCredentials
                        {
                            Username = "iconnectsolutions",
                            Password = azureDevOpsToken
                        }
                };

                // Push all branches to Azure DevOps
                repo.Network.Push(remote, @"refs/heads/*", pushOptions);
            }

            return "true";
        }
        catch (Exception ex)
        {
            return $"false: {ex.Message}";
        }
    }
}

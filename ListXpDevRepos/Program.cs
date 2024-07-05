using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

class Program
{
    static HttpClient client = new HttpClient();

    static async Task Main(string[] args)
    {
        string xpDevBaseUrl = "https://iconnect.xp-dev.com/api/v1"; // XPDev API base URL
        string xpDevToken = "8e0da004f615b684d935242dc8d448ee"; // XPDev token
        string azureDevOpsOrganization = "iconnectsolutions"; // Replace with your Azure DevOps organization
        string azureDevOpsProject = "Repos"; // Replace with your Azure DevOps project name
        string azureDevOpsToken = "jz74m2hym3x3oegf7zrkzc4hvabxa3z5sfqx5n6oul5dwhvwjd4q"; // Replace with your Azure DevOps PAT

        client.DefaultRequestHeaders.Add("X-XPDevToken", xpDevToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{azureDevOpsToken}")));

        await ListProjects(xpDevBaseUrl, azureDevOpsOrganization, azureDevOpsProject);
    }

    static async Task ListProjects(string xpDevBaseUrl, string azureDevOpsOrganization, string azureDevOpsProject)
    {
        string projectsUrl = $"{xpDevBaseUrl}/projects";

        HttpResponseMessage response = await client.GetAsync(projectsUrl);

        if (response.IsSuccessStatusCode)
        {
            string projectsJson = await response.Content.ReadAsStringAsync();
            JArray projectsArray = JArray.Parse(projectsJson);
            foreach (var project in projectsArray)
            {
                string projectId = project["id"].ToString();
                string projectName = project["name"].ToString();
                await ListRepositories(xpDevBaseUrl, azureDevOpsOrganization, azureDevOpsProject, projectName, projectId);
            }
        }
    }

    static async Task ListRepositories(string xpDevBaseUrl, string azureDevOpsOrganization, string azureDevOpsProject, string projectName, string projectId)
    {
        string repositoriesUrl = $"{xpDevBaseUrl}/repositories/project/{projectId}";

        HttpResponseMessage response = await client.GetAsync(repositoriesUrl);

        if (response.IsSuccessStatusCode)
        {
            string repositoriesJson = await response.Content.ReadAsStringAsync();
            JArray repositoriesArray = JArray.Parse(repositoriesJson);

            foreach (var repository in repositoriesArray)
            {
                string repoName = repository["name"].ToString();
                string repoType = repository["type"].ToString();

                if (repoType.Equals("Git", StringComparison.OrdinalIgnoreCase))
                {
                    if (repoName == "rec-trac-ui")
                    {
                        Console.WriteLine("_________________________________________________________________________________________________________");
                        Console.WriteLine($"ProjectName: {projectName}");
                        Console.WriteLine($"RepoName: {repoName}");

                        string cloneResult = await CloneAndPushGitRepository(projectName, repoName, azureDevOpsOrganization, azureDevOpsProject);

                        Console.WriteLine($"Clone and Push Result: {cloneResult}");
                        Console.WriteLine("_________________________________________________________________________________________________________");
                    }
                }
            }
        }
    }

    static async Task<string> CloneAndPushGitRepository(string projectName, string repoName, string organization, string project)
    {
        string baseDir = "D:\\XpDevRepositories";
        string cloneDir = Path.Combine(baseDir, projectName, repoName);
        string azureRepoUrl = $"https://dev.azure.com/{organization}/{project}/_git/{repoName}";

        try
        {
            // Clone repository
            ProcessStartInfo cloneInfo = new ProcessStartInfo
            {
                FileName = @"C:\Program Files\Git\bin\git.exe",
                Arguments = $"clone --depth=1 https://iconnect.xp-dev.com/git/{repoName} {cloneDir}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process cloneProcess = Process.Start(cloneInfo))
            {
                if (cloneProcess != null)
                {
                    await cloneProcess.StandardOutput.ReadToEndAsync();
                    await cloneProcess.StandardError.ReadToEndAsync();
                    cloneProcess.WaitForExit();

                    if (cloneProcess.ExitCode != 0)
                    {
                        return "false: Clone failed";
                    }
                }
            }

            // Fetch all remote branches
            ProcessStartInfo fetchInfo = new ProcessStartInfo
            {
                FileName = @"C:\Program Files\Git\bin\git.exe",
                Arguments = "fetch --all --tags",
                WorkingDirectory = cloneDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process fetchProcess = Process.Start(fetchInfo))
            {
                if (fetchProcess != null)
                {
                    await fetchProcess.StandardOutput.ReadToEndAsync();
                    await fetchProcess.StandardError.ReadToEndAsync();
                    fetchProcess.WaitForExit();

                    if (fetchProcess.ExitCode != 0)
                    {
                        return "false: Fetch failed";
                    }
                }
            }

            // List all remote branches
            ProcessStartInfo listBranchesInfo = new ProcessStartInfo
            {
                FileName = @"C:\Program Files\Git\bin\git.exe",
                Arguments = "branch -r",
                WorkingDirectory = cloneDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process listBranchesProcess = Process.Start(listBranchesInfo))
            {
                if (listBranchesProcess != null)
                {
                    string branchesOutput = await listBranchesProcess.StandardOutput.ReadToEndAsync();
                    await listBranchesProcess.StandardError.ReadToEndAsync();
                    listBranchesProcess.WaitForExit();

                    if (listBranchesProcess.ExitCode != 0)
                    {
                        return "false: Failed to list branches";
                    }

                    // Split branches output by newline
                    string[] branches = branchesOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    // Push each branch to Azure DevOps
                    foreach (var branch in branches)
                    {
                        if (branch.StartsWith("origin/") && !branch.Contains("HEAD ->"))
                        {
                            string branchName = branch.Substring("origin/".Length);

                            ProcessStartInfo pushBranchInfo = new ProcessStartInfo
                            {
                                FileName = @"C:\Program Files\Git\bin\git.exe",
                                Arguments = $"push origin {branchName}",
                                WorkingDirectory = cloneDir,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using (Process pushBranchProcess = Process.Start(pushBranchInfo))
                            {
                                if (pushBranchProcess != null)
                                {
                                    await pushBranchProcess.StandardOutput.ReadToEndAsync();
                                    await pushBranchProcess.StandardError.ReadToEndAsync();
                                    pushBranchProcess.WaitForExit();

                                    if (pushBranchProcess.ExitCode != 0)
                                    {
                                        return $"false: Push failed for branch {branchName}";
                                    }
                                }
                            }
                        }
                    }

                    return "true: All branches pushed successfully";
                }
            }

            return "false: Failed to list branches";
        }
        catch (Exception ex)
        {
            return $"false: {ex.Message}";
        }
    }
}

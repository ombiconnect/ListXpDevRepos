using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SharpSvn;

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

                if (repoType.Equals("Subversion", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("_________________________________________________________________________________________________________");
                    Console.WriteLine("ProjectName =" + projectName);
                    Console.WriteLine("RepoName =" + repoName);
                    string IsClone = await CloneSubversionRepository(projectName, repoName);
                    Console.WriteLine("IsClone =" + IsClone);
                    Console.WriteLine("_________________________________________________________________________________________________________");

                }

                if (repoType.Equals("Git", StringComparison.OrdinalIgnoreCase))
                {


                        Console.WriteLine("_________________________________________________________________________________________________________");
                        Console.WriteLine("ProjectName =" + projectName);
                        Console.WriteLine("RepoName =" + repoName);
                        string IsClone = await CloneGitRepository(projectName, repoName);
                        Console.WriteLine("IsClone =" + IsClone);
                        if (IsClone == "true")
                        {
                            string IsCreatedAzureDirectory = await CreateAzureDevOpsRepository(azureDevOpsOrganization, azureDevOpsProject, repoName);
                            Console.WriteLine("IsCreatedAzureDirectory= " + IsCreatedAzureDirectory);
                            string IsPushed = await PushToAzureDevOps(projectName, repoName, azureDevOpsOrganization, azureDevOpsProject, repoName);
                            Console.WriteLine("IsPushed " + IsPushed);
                            Console.WriteLine("_________________________________________________________________________________________________________");

                        }
                    
                }
                }

            }
        }
    
    static async Task<string> CloneSubversionRepository(string projectName, string repoName)
    {
        string baseDir = "D:\\XpDevRepositories\\SubversionRepos";
        string cloneDir = Path.Combine(baseDir, projectName, repoName);

        try
        {
            // Create the clone directory if it does not exist
            if (!Directory.Exists(cloneDir))
            {
                Directory.CreateDirectory(cloneDir);
            }

            SvnUriTarget target = new SvnUriTarget(new Uri($"https://iconnect.xp-dev.com/svn/{repoName}"));
            using (SvnClient client = new SvnClient())
            {
                client.Authentication.DefaultCredentials = new System.Net.NetworkCredential("omb", "ruZTUN46");
                client.CheckOut(target, cloneDir);
            }
            return "true";
            //Console.WriteLine($"SVN repository cloned successfully: {repoName}");
        }
        catch (Exception ex)
        {
            //Console.WriteLine($"Error cloning repository {repoName}: {ex.Message}");
            return "false " + ex;
        }
    }
    static async Task<string> CloneGitRepository(string projectName, string repoName)
    {
        string baseDir = "D:\\XpDevRepositories";
        string cloneDir = Path.Combine(baseDir, projectName, repoName);

        try
        {
            if (!Directory.Exists(cloneDir))
            {
                Directory.CreateDirectory(cloneDir);
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = @"C:\Program Files\Git\bin\git.exe",
                //Arguments = $"clone https://iconnect.xp-dev.com/git/{repoName} {cloneDir}",
                //Arguments = $"clone \"https://iconnect.xp-dev.com/git/{repoName}\" \"{cloneDir}\"",
                Arguments = $"clone --depth=1 \"https://iconnect.xp-dev.com/git/{repoName}\" \"{cloneDir}\"",


                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            //https://iconnect.xp-dev.com/git/procurement-mgmt-POC
            using (Process process = Process.Start(psi))
            {
                if (process != null)
                {
                    //await process.StandardOutput.ReadToEndAsync();
                    //await process.StandardError.ReadToEndAsync();
                    //process.WaitForExit();

                    //return process.ExitCode == 0 ? "true" : "false";

                    Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> errorTask = process.StandardError.ReadToEndAsync();

                    await Task.WhenAll(outputTask, errorTask); // Wait for both tasks to complete

                    process.WaitForExit();

                    // Check exit code and handle success/failure
                    if (process.ExitCode == 0)
                    {
                        return "true";
                    }
                    else
                    {
                        string errorMessage = $"Git clone failed wi";
                    }

                }

            }

        }
        catch (Exception ex)
        {
            return "false " + ex;
        }
        return "false";
    }
    //static async Task<string> CloneGitRepository(string projectName, string repoName)
    //{
    //    string baseDir = "D:\\XpDevRepositories";
    //    string cloneDir = Path.Combine(baseDir, projectName, repoName);

    //    try
    //    {
    //        if (!Directory.Exists(cloneDir))
    //        {
    //            Directory.CreateDirectory(cloneDir);
    //        }

    //        ProcessStartInfo cloneInfo = new ProcessStartInfo
    //        {
    //            FileName = @"C:\Program Files\Git\bin\git.exe",
    //            Arguments = $"clone \"https://iconnect.xp-dev.com/git/{repoName}\" \"{cloneDir}\"",
    //            RedirectStandardOutput = true,
    //            RedirectStandardError = true,
    //            UseShellExecute = false,
    //            CreateNoWindow = true
    //        };

    //        using (Process cloneProcess = Process.Start(cloneInfo))
    //        {
    //            if (cloneProcess != null)
    //            {
    //                string output = await cloneProcess.StandardOutput.ReadToEndAsync();
    //                string error = await cloneProcess.StandardError.ReadToEndAsync();
    //                cloneProcess.WaitForExit();

    //                if (cloneProcess.ExitCode != 0)
    //                {
    //                    return $"false: {error}";
    //                }
    //            }
    //        }

    //        ProcessStartInfo fetchInfo = new ProcessStartInfo
    //        {
    //            FileName = @"C:\Program Files\Git\bin\git.exe",
    //            Arguments = "fetch --all",
    //            WorkingDirectory = cloneDir,
    //            RedirectStandardOutput = true,
    //            RedirectStandardError = true,
    //            UseShellExecute = false,
    //            CreateNoWindow = true
    //        };

    //        using (Process fetchProcess = Process.Start(fetchInfo))
    //        {
    //            if (fetchProcess != null)
    //            {
    //                string output = await fetchProcess.StandardOutput.ReadToEndAsync();
    //                string error = await fetchProcess.StandardError.ReadToEndAsync();
    //                fetchProcess.WaitForExit();

    //                if (fetchProcess.ExitCode != 0)
    //                {
    //                    return $"false: {error}";
    //                }
    //            }
    //        }

    //        ProcessStartInfo pullInfo = new ProcessStartInfo
    //        {
    //            FileName = @"C:\Program Files\Git\bin\git.exe",
    //            Arguments = "pull --all",
    //            WorkingDirectory = cloneDir,
    //            RedirectStandardOutput = true,
    //            RedirectStandardError = true,
    //            UseShellExecute = false,
    //            CreateNoWindow = true
    //        };

    //        using (Process pullProcess = Process.Start(pullInfo))
    //        {
    //            if (pullProcess != null)
    //            {
    //                string output = await pullProcess.StandardOutput.ReadToEndAsync();
    //                string error = await pullProcess.StandardError.ReadToEndAsync();
    //                pullProcess.WaitForExit();

    //                if (pullProcess.ExitCode != 0)
    //                {
    //                    return $"false: {error}";
    //                }
    //            }
    //        }

    //        return "true";
    //    }
    //    catch (Exception ex)
    //    {
    //        return $"false: {ex.Message}";
    //    }
    //}


    static async Task<string> CreateAzureDevOpsRepository(string organization, string project, string repoName)
    {
        string createRepoUrl = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories?api-version=6.0";

        var repoData = new
        {
            name = repoName
        };

        StringContent content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(repoData), Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync(createRepoUrl, content);

        return response.IsSuccessStatusCode ? "true" : "false";
    }

    static async Task<string> PushToAzureDevOps(string projectName, string repoName, string organization, string project, string newRepoName)
    {
        string baseDir = "D:\\XpDevRepositories";
        string cloneDir = Path.Combine(baseDir, projectName, repoName);
        string azureRepoUrl = $"https://{organization}@dev.azure.com/{organization}/{project}/_git/{newRepoName}";

        try
        {
            ProcessStartInfo addRemoteInfo = new ProcessStartInfo
            {
                FileName = @"C:\Program Files\Git\bin\git.exe",
                Arguments = $"remote add origin {azureRepoUrl}",
                WorkingDirectory = cloneDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process addRemoteProcess = Process.Start(addRemoteInfo))
            {
                if (addRemoteProcess != null)
                {
                    await addRemoteProcess.StandardOutput.ReadToEndAsync();
                    await addRemoteProcess.StandardError.ReadToEndAsync();
                    addRemoteProcess.WaitForExit();
                    Task<string> outputTask = addRemoteProcess.StandardOutput.ReadToEndAsync();
                    Task<string> errorTask = addRemoteProcess.StandardError.ReadToEndAsync();

                    await Task.WhenAll(outputTask, errorTask); // Wait for both tasks to complete

                    addRemoteProcess.WaitForExit();

                    if (addRemoteProcess.ExitCode != 0)
                    {
                        return "false";
                    }
                }
            }

            ProcessStartInfo pushInfo = new ProcessStartInfo
            {
                FileName = @"C:\Program Files\Git\bin\git.exe",
                Arguments = "push origin --all",
                WorkingDirectory = cloneDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process pushProcess = Process.Start(pushInfo))
            {
                if (pushProcess != null)
                {
                    await pushProcess.StandardOutput.ReadToEndAsync();
                    await pushProcess.StandardError.ReadToEndAsync();
                    pushProcess.WaitForExit();

                    return pushProcess.ExitCode == 0 ? "true" : "false";
                }
            }
        }
        catch (Exception ex)
        {
            return "false " + ex;
        }
        return "false";
    }
}



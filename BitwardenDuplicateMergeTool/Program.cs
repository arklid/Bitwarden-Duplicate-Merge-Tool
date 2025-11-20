/*
                                           ▄▄▄▄▄▄▄▄
                               ▄▄▄▀▀▀█    █▀ ▄▄▄▄ █
               ▄▄▄▄▄▄         ▄▀ ▄▄█ █    █ ▓███  ▀▀█  ▄▄▄▄▄▄▄▄
          ▄▄▄█▀▀ ▄▄ ▀▀█▄  ▄▄▄▄█ ▓███ █▄▄▄█▌▐███▌ ▀▀ ▀█ █ ▄▄▄▄ █▄
     ▄ ▄█▀▀ ▄▄▄██▀█▓▓▄ ██▀▀ ▄▄  ▀▓██▌▀ ▄▄▄ ▓███ ▐███ █▄█▌▐███▌ ▀▀▀▀ ▀
         ▄▓▓██▀  ▐██▓▌ ▀▄▄██▀██▓▄▀██▌ ▓██ ▄███▌ ▓██▌  ▄▄▄▄███▓ !fa
         ▐▓██▌ ▀▀████ ▓▓██▀ ▐██▓▌▐███▄██▄ ▐███ ███▌ ▄███▀ ▐██▓▌
      ▄   ▓███ ░▐███▌▀▓██▌ ▄███▀ ███ ▀██▓▓ ██▌ ███ ▐▓██▌ ▄██▓▓▀ ▀
        █ ▐███▌ ▀▀▀▀  ▓███  ▀▀  ███▌  ▐██▓▌▓██ ▀ ▄ ▀▓██▄██▀▀  ▄▄▀
        ▀█ ▀▀▀▀ ▄█▀▀█ ▐███▌ ▀  ▄███▌▀  ███▓ ▀▀▀▀ ▄▄▄  ▀▀  ▄▄▄█▀
         ▀▀▀▀▀▀▀▀   ▀█ ▀▀▀▀ █▄▄ ▀▀▀ ▄ ▐███▌ ▄█▀▀▀▀ ▀▀▀▀▀▀▀▀
                      ▀▀▀▀▀▀▀▀ ▀▀▀▀▀▀█ ▀▀▀▀ ▄█ fredric arklid presents
                                     ▀▀▀▀▀▀▀▀
 */

using BitwardenDuplicateMergeTool.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BitwardenDuplicateMergeTool
{
    /// <summary>
    /// Command line entrypoint for the Bitwarden Duplicate Merge Tool.
    /// Provides interactive and non-interactive flows for analyzing and merging duplicates
    /// from a Bitwarden JSON export.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Application entry point.
        /// Parses a Bitwarden exported JSON file and runs analysis or deterministic merging.
        /// </summary>
        /// <param name="args">Command-line arguments (currently ignored; the CLI prompts for input interactively).</param>
        /// <returns>Process exit code: 0 on success, non-zero on error.</returns>
        public static async Task<int> Main(string[] args)
        {
            Console.WriteLine("Bitwarden Duplicate Merge Tool - CLI (MVP)");
            Console.WriteLine("Load vault from local exported JSON file");
            Console.Write("Path to exported Bitwarden JSON file: ");

            var path = Console.ReadLine() ?? "";

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Console.WriteLine("File not found.");
                return 1;
            }

            string json;

            try
            {
                json = await File.ReadAllTextAsync(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read file: {ex.Message}");
                return 1;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            VaultRoot originalVault;
            VaultRoot? vault;

            try
            {
                // Keep the original parsed vault for later diffing/reporting,
                // and create a separate instance for processing so we can compare before/after.
                originalVault = JsonSerializer.Deserialize<VaultRoot>(json, options) ?? new VaultRoot();
                vault = JsonSerializer.Deserialize<VaultRoot>(json, options) ?? new VaultRoot();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse vault JSON: {ex.Message}");
                return 1;
            }

            Console.WriteLine($"Loaded vault: folders={vault.Folders?.Count ?? 0}, items={vault.Items?.Count ?? 0}");

            Console.WriteLine();
            Console.WriteLine("Choose action:");
            Console.WriteLine("  [A] Remove duplicates only (default)");
            Console.WriteLine("  [M] Remove duplicates and merge data from duplicate items (deterministic rules)");
            Console.Write("Select action (A/M): ");

            var choice = (Console.ReadLine() ?? "").Trim();
            var mergeMode = string.Equals(choice, "M", StringComparison.OrdinalIgnoreCase);

            Console.Write("Verbose output? (y/N): ");

            var verbose = (Console.ReadLine() ?? "").Trim();
            var verboseMode = string.Equals(verbose, "y", StringComparison.OrdinalIgnoreCase);

            Console.WriteLine(mergeMode ? "Running automatic merge of duplicates..." : "Analyzing duplicates...");

            var summary = Deduplicator.Deduplicate(vault, mergeMode, verboseMode);

            Console.WriteLine(
                $"Result: original folders={summary.OriginalFolders}, deduplicated folders={summary.DeduplicatedFolders} (removed {summary.RemovedFolders})");
            
            Console.WriteLine(
                $"Result: original items={summary.OriginalItems}, deduplicated items={summary.DeduplicatedItems} (removed {summary.RemovedItems})");

            // If verbose mode is enabled, print a visual diff of changes (colored console output).
            if (verboseMode)
            {
                try
                {
                    Console.WriteLine();
                    Console.WriteLine("Visual diff of changes (verbose):");

                    var originalFolders = originalVault.Folders ?? [];
                    var dedupFolders = vault.Folders ?? [];

                    var removedFolderList = originalFolders
                        .Where(of => !dedupFolders.Any(nf =>
                            string.Equals(nf.Id, of.Id, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(nf.Name, of.Name, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    var addedFolderList = dedupFolders
                        .Where(nf => !originalFolders.Any(of =>
                            string.Equals(of.Id, nf.Id, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(of.Name, nf.Name, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    var originalItems = originalVault.Items ?? [];
                    var dedupItems = vault.Items ?? [];

                    var removedItemList = originalItems
                        .Where(oi =>
                            !dedupItems.Any(ni => string.Equals(ni.Id, oi.Id, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    var addedItemList = dedupItems
                        .Where(ni =>
                            !originalItems.Any(oi => string.Equals(oi.Id, ni.Id, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    var remappedItems = originalItems
                        .Select(oi => new
                        {
                            Orig = oi,
                            New = dedupItems.FirstOrDefault(ni =>
                                string.Equals(ni.Id, oi.Id, StringComparison.OrdinalIgnoreCase))
                        })
                        .Where(x => x.New != null && !string.Equals(x.Orig.FolderId ?? "", x.New.FolderId ?? "",
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // Folders
                    if (removedFolderList.Count == 0 && addedFolderList.Count == 0)
                    {
                        Console.WriteLine("  No folder additions or removals");
                    }
                    else
                    {
                        foreach (var f in removedFolderList)
                        {
                            var prev = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"- Folder removed: name='{f.Name}' id='{f.Id}'");
                            Console.ForegroundColor = prev;
                        }

                        foreach (var f in addedFolderList)
                        {
                            var prev = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"+ Folder added:   name='{f.Name}' id='{f.Id}'");
                            Console.ForegroundColor = prev;
                        }
                    }

                    Console.WriteLine();

                    // Items
                    if (removedItemList.Count == 0 && addedItemList.Count == 0)
                    {
                        Console.WriteLine("  No item additions or removals");
                    }
                    else
                    {
                        foreach (var it in removedItemList)
                        {
                            var prev = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(
                                $"- Item removed:   id='{it.Id}' name='{it.Name}' username='{it.Login?.Username}'");
                            Console.ForegroundColor = prev;
                        }

                        foreach (var it in addedItemList)
                        {
                            var prev = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(
                                $"+ Item added:     id='{it.Id}' name='{it.Name}' username='{it.Login?.Username}'");
                            Console.ForegroundColor = prev;
                        }
                    }

                    Console.WriteLine();

                    // Remapped folder IDs for items (show as - / + pair)
                    if (remappedItems.Count == 0)
                    {
                        Console.WriteLine("  No folder remappings detected");
                    }
                    else
                    {
                        foreach (var r in remappedItems)
                        {
                            var prev = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(
                                $"- Remapped item: id='{r.Orig.Id}' folderId='{r.Orig.FolderId ?? "<null>"}'");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(
                                $"+ Remapped item: id='{r.New?.Id}' folderId='{r.New?.FolderId ?? "<null>"}'");
                            Console.ForegroundColor = prev;
                        }
                    }

                    Console.WriteLine();
                }
                catch
                {
                    // Don't fail the whole run for visualization errors; fall back to non-colored output.
                    Console.WriteLine("Failed to produce verbose visual diff (falling back to text-only).");
                }
            }

            Console.WriteLine();

            // Offer interactive inspection of duplicate groups (enhanced command set)
            var groups = Deduplicator.FindDuplicateGroups(vault);

            if (groups.Count > 0)
            {
                Console.WriteLine($"Found {groups.Count} duplicate group(s).");
                Console.Write("Inspect duplicates interactively? (y/N): ");

                var inspect = (Console.ReadLine() ?? "").Trim();

                if (string.Equals(inspect, "y", StringComparison.OrdinalIgnoreCase))
                {
                    var itemsList = vault.Items?.ToList() ?? [];
                    var quitAll = false;

                    // Local thin wrappers that delegate to public static helpers (exposed for unit testing)
                    List<int> ParseIndexSet(string input, int maxExclusive) => ParseIndexSet(input, maxExclusive);

                    void ShowItemDetails(Item? it) => Console.WriteLine(FormatItemDetails(it));

                    for (var gIdx = 0; gIdx < groups.Count && !quitAll; gIdx++)
                    {
                        var g = groups[gIdx];

                        while (!quitAll)
                        {
                            Console.WriteLine(new string('-', 60));
                            Console.WriteLine($"Group {gIdx + 1}/{groups.Count} key: {g.Key} (count={g.Items.Count})");

                            for (var i = 0; i < g.Items.Count; i++)
                            {
                                var it = g.Items[i];
                                var uris = it.Login?.Uris != null
                                    ? string.Join(", ", it.Login.Uris.Select(u => u.Uri))
                                    : "";

                                Console.WriteLine(
                                    $"[{i}] id={it.Id} name={it.Name} username={it.Login?.Username} uris={uris}");
                            }

                            Console.WriteLine();
                            Console.WriteLine("Commands:");
                            Console.WriteLine("  v <idx|list>   View item details (e.g. v 0, v 0-2)");
                            Console.WriteLine("  m <list>       Merge selected items (provide two or more indices)");
                            Console.WriteLine("  d <list>       Delete selected items from vault");
                            Console.WriteLine("  k              Keep first item in group (remove others)");
                            Console.WriteLine("  s or n         Skip to next group");
                            Console.WriteLine("  p              Go to previous group");
                            Console.WriteLine("  q              Quit interactive mode");
                            Console.Write("Enter command: ");

                            var cmdLine = (Console.ReadLine() ?? "").Trim();

                            if (string.IsNullOrWhiteSpace(cmdLine))
                            {
                                Console.WriteLine("No command entered. Skipping this group.");
                                break;
                            }

                            var tokens = cmdLine.Split(' ', 2,
                                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            var cmd = tokens[0].ToLowerInvariant();
                            var arg = tokens.Length > 1 ? tokens[1] : "";

                            if (cmd == "v")
                            {
                                var ids = ParseIndexSet(arg, g.Items.Count);

                                if (ids.Count == 0)
                                    Console.WriteLine("No valid indices to view.");
                                else
                                {
                                    foreach (var idx in ids)
                                    {
                                        ShowItemDetails(g.Items[idx]);
                                        Console.WriteLine();
                                    }
                                }

                                // stay in the same group after viewing
                                continue;
                            }
                            else if (cmd == "m")
                            {
                                var ids = ParseIndexSet(arg, g.Items.Count);

                                if (ids.Count < 2)
                                {
                                    Console.WriteLine(
                                        "Need at least two items to merge (provide two or more indices).");
                                    continue;
                                }

                                var itemsToMerge = ids.Select(i => g.Items[i]).ToList();
                                var merged = itemsToMerge.Aggregate((a, b) => Deduplicator.MergeItems(a, b));

                                // Replace the first selected occurrence in the global itemsList (match by Id)
                                var firstId = itemsToMerge.First()?.Id;
                                var idxInList = itemsList.FindIndex(x => x.Id == firstId);

                                if (idxInList >= 0) itemsList[idxInList] = merged;

                                var removeIds = itemsToMerge.Skip(1).Select(x => x.Id)
                                    .Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet();

                                itemsList = itemsList.Where(x => !removeIds.Contains(x.Id)).ToList();

                                // Update group to reflect removed items
                                g.Items = g.Items.Where(x => !removeIds.Contains(x?.Id)).ToList();

                                Console.WriteLine($"Merged {ids.Count} items into id='{merged?.Id}'");

                                // Move on to next group
                                break;
                            }
                            else if (cmd == "d")
                            {
                                var ids = ParseIndexSet(arg, g.Items.Count);

                                if (ids.Count == 0)
                                {
                                    Console.WriteLine("No valid indices to delete.");
                                    continue;
                                }

                                var toDelete = ids.Select(i => g.Items[i]).ToList();
                                var deleteIds = toDelete.Select(x => x.Id).Where(x => !string.IsNullOrWhiteSpace(x))
                                    .ToHashSet();
                                itemsList = itemsList.Where(x => !deleteIds.Contains(x.Id)).ToList();
                                g.Items = g.Items.Where(x => !deleteIds.Contains(x.Id)).ToList();
                                Console.WriteLine($"Deleted {deleteIds.Count} item(s) from vault.");
                                
                                // If group reduced to <=1, move on
                                if (g.Items.Count <= 1) break;

                                continue;
                            }
                            else if (cmd == "k")
                            {
                                var removeIds = g.Items.Skip(1).Select(x => x.Id)
                                    .Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet();

                                itemsList = itemsList.Where(x => !removeIds.Contains(x.Id)).ToList();
                                g.Items = g.Items.Take(1).ToList();

                                Console.WriteLine("Kept first item and removed others in the group.");
                                break;
                            }
                            else if (cmd == "s" || cmd == "n")
                            {
                                Console.WriteLine("Skipping to next group.");
                                break;
                            }
                            else if (cmd == "p")
                            {
                                // Move back one group (the for loop will increment it again)
                                gIdx = Math.Max(-1, gIdx - 2);
                                break;
                            }
                            else if (cmd == "q")
                            {
                                quitAll = true;
                                break;
                            }
                            else
                            {
                                Console.WriteLine("Unknown command; try again.");
                                continue;
                            }
                        } // end while for current group
                    } // end for groups

                    vault.Items = itemsList;

                    Console.WriteLine("Interactive deduplication complete.");
                }
                else
                {
                    Console.WriteLine("Skipping interactive inspection.");
                }
            }
            else
            {
                Console.WriteLine("No duplicate groups found.");
            }

            Console.Write("Save deduplicated vault to file (leave empty to skip): ");
            var outPath = Console.ReadLine();

            if (!string.IsNullOrWhiteSpace(outPath))
            {
                try
                {
                    // Serialize deduplicated vault and save JSON
                    EnsureNonNullProperties(vault);
                    var outJson = JsonSerializer.Serialize(vault, options);
                    File.WriteAllText(outPath, outJson, new UTF8Encoding(false));
                    Console.WriteLine($"Saved deduplicated vault to {outPath}");

                    // Generate Markdown summary alongside the output JSON
                    var originalSize = new UTF8Encoding(false).GetByteCount(json);
                    var deduplicatedSize = new UTF8Encoding(false).GetByteCount(outJson);
                    var md = new StringBuilder();

                    md.AppendLine("# Bitwarden Deduplication Summary");
                    md.AppendLine();
                    md.AppendLine("## Original File");
                    md.AppendLine($"- Size: {originalSize:N0} bytes ({originalSize / 1024.0 / 1024.0:F2} MB)");
                    md.AppendLine($"- Folders: {summary.OriginalFolders}");
                    md.AppendLine($"- Items: {summary.OriginalItems}");
                    md.AppendLine();
                    md.AppendLine("## Deduplicated File");
                    md.AppendLine($"- Size: {deduplicatedSize:N0} bytes ({deduplicatedSize / 1024.0 / 1024.0:F2} MB)");
                    md.AppendLine($"- Folders: {summary.DeduplicatedFolders}");
                    md.AppendLine($"- Items: {summary.DeduplicatedItems}");
                    md.AppendLine();
                    md.AppendLine("## Results");

                    var removedFolders = summary.RemovedFolders;
                    var removedItems = summary.RemovedItems;
                    var folderReduction = summary.OriginalFolders > 0
                        ? Math.Round((double)removedFolders / summary.OriginalFolders * 100, 1)
                        : 0;
                    var itemReduction = summary.OriginalItems > 0
                        ? Math.Round((double)removedItems / summary.OriginalItems * 100, 1)
                        : 0;
                    var sizeReduction = originalSize - deduplicatedSize;
                    var sizeReductionPercent =
                        originalSize > 0 ? Math.Round((double)sizeReduction / originalSize * 100, 1) : 0;

                    md.AppendLine($"- Removed {removedFolders} duplicate folders ({folderReduction}% reduction)");
                    md.AppendLine($"- Removed {removedItems} duplicate items ({itemReduction}% reduction)");
                    md.AppendLine(
                        $"- Reduced file size by {sizeReduction:N0} bytes ({sizeReductionPercent}% reduction)");
                    md.AppendLine();
                    md.AppendLine("## Method");
                    md.AppendLine("- Folders deduplicated by case-insensitive name.");
                    md.AppendLine("- Items grouped by composite key: name | type | username | URIs.");
                    md.AppendLine(
                        "- In merge mode: scalar values prefer non-empty, URIs and fields are merged uniquely.");
                    md.AppendLine();
                    md.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    md.AppendLine();
                    md.AppendLine("## Visual diff (removed = red, added = green)");
                    md.AppendLine();

                    // Compute simple before/after sets for a visual diff
                    var originalFolders = originalVault.Folders ?? [];
                    var dedupFolders = vault.Folders ?? [];
                    var removedFolderList = originalFolders
                        .Where(of => !dedupFolders.Any(nf =>
                            string.Equals(nf.Id, of.Id, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(nf.Name, of.Name, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    var addedFolderList = dedupFolders
                        .Where(nf => !originalFolders.Any(of =>
                            string.Equals(of.Id, nf.Id, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(of.Name, nf.Name, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    var originalItems = originalVault.Items ?? [];
                    var dedupItems = vault.Items ?? [];
                    var removedItemList = originalItems
                        .Where(oi =>
                            !dedupItems.Any(ni => string.Equals(ni.Id, oi.Id, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    var addedItemList = dedupItems
                        .Where(ni =>
                            !originalItems.Any(oi => string.Equals(oi.Id, ni.Id, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    var remappedItems = originalItems
                        .Select(oi => new
                        {
                            Orig = oi,
                            New = dedupItems.FirstOrDefault(ni =>
                                string.Equals(ni.Id, oi.Id, StringComparison.OrdinalIgnoreCase))
                        })
                        .Where(x => x.New != null && !string.Equals(x.Orig.FolderId ?? "", x.New.FolderId ?? "",
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    md.AppendLine("```diff");
                    if (removedFolderList.Count == 0 && addedFolderList.Count == 0)
                    {
                        md.AppendLine("  No folder additions or removals");
                    }
                    else
                    {
                        foreach (var f in removedFolderList)
                            md.AppendLine($"- Folder removed: name='{f.Name}' id='{f.Id}'");
                        foreach (var f in addedFolderList)
                            md.AppendLine($"+ Folder added:   name='{f.Name}' id='{f.Id}'");
                    }

                    md.AppendLine();
                    if (removedItemList.Count == 0 && addedItemList.Count == 0)
                    {
                        md.AppendLine("  No item additions or removals");
                    }
                    else
                    {
                        foreach (var it in removedItemList)
                            md.AppendLine(
                                $"- Item removed:   id='{it.Id}' name='{it.Name}' username='{it.Login?.Username}'");
                        foreach (var it in addedItemList)
                            md.AppendLine(
                                $"+ Item added:     id='{it.Id}' name='{it.Name}' username='{it.Login?.Username}'");
                    }

                    md.AppendLine();

                    if (remappedItems.Count == 0)
                    {
                        md.AppendLine("  No folder remapping detected");
                    }
                    else
                    {
                        foreach (var r in remappedItems)
                        {
                            md.AppendLine(
                                $"- Remapped item: id='{r.Orig?.Id}' folderId='{r.Orig.FolderId ?? "<null>"}'");
                            md.AppendLine($"+ Remapped item: id='{r.New?.Id}' folderId='{r.New?.FolderId ?? "<null>"}'");
                        }
                    }

                    md.AppendLine("```");

                    // Also print a colored visual diff to the console for immediate visibility:
                    Console.WriteLine();
                    Console.WriteLine("Visual diff (removed = red, added = green):");

                    if (removedFolderList.Count == 0 && addedFolderList.Count == 0)
                    {
                        Console.WriteLine("  No folder additions or removals");
                    }
                    else
                    {
                        foreach (var f in removedFolderList)
                        {
                            var prev = Console.ForegroundColor;

                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"- Folder removed: name='{f.Name}' id='{f.Id}'");
                            Console.ForegroundColor = prev;
                        }

                        foreach (var f in addedFolderList)
                        {
                            var prev = Console.ForegroundColor;

                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"+ Folder added:   name='{f.Name}' id='{f.Id}'");
                            Console.ForegroundColor = prev;
                        }
                    }

                    Console.WriteLine();

                    if (removedItemList.Count == 0 && addedItemList.Count == 0)
                    {
                        Console.WriteLine("  No item additions or removals");
                    }
                    else
                    {
                        foreach (var it in removedItemList)
                        {
                            var prev = Console.ForegroundColor;

                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(
                                $"- Item removed:   id='{it.Id}' name='{it.Name}' username='{it.Login?.Username}'");
                            Console.ForegroundColor = prev;
                        }

                        foreach (var it in addedItemList)
                        {
                            var prev = Console.ForegroundColor;

                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(
                                $"+ Item added:     id='{it?.Id}' name='{it.Name}' username='{it.Login?.Username}'");
                            Console.ForegroundColor = prev;
                        }
                    }

                    Console.WriteLine();
                    if (remappedItems.Count == 0)
                    {
                        Console.WriteLine("  No folder remapping detected");
                    }
                    else
                    {
                        foreach (var r in remappedItems)
                        {
                            var prev = Console.ForegroundColor;

                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(
                                $"- Remapped item: id='{r.Orig?.Id}' folderId='{r.Orig?.FolderId ?? "<null>"}'");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(
                                $"+ Remapped item: id='{r.New?.Id}' folderId='{r.New?.FolderId ?? "<null>"}'");
                            Console.ForegroundColor = prev;
                        }
                    }

                    var summaryPath = Path.ChangeExtension(outPath, ".md");
                    File.WriteAllText(summaryPath, md.ToString(), new UTF8Encoding(false));
                    Console.WriteLine($"Saved summary to {summaryPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to save file or summary: {ex.Message}");
                }
            }

            Console.WriteLine("Done.");

            return 0;
        }

        /// <summary>
        /// Convert empty string scalar properties within the vault to null values.
        /// This normalization is performed in-place so that JSON serialization will
        /// emit nulls (preserving the property) instead of empty strings.
        /// </summary>
        /// <param name="vault">Vault to normalize. If null the method returns immediately.</param>
        static void EnsureNonNullProperties(VaultRoot? vault)
        {
            if (vault == null) return;

            // Folders: if present convert empty strings to null (leave folder collection null if it was missing)
            if (vault.Folders != null)
            {
                foreach (var f in vault.Folders)
                {
                    if (f == null) continue;

                    f.Id = string.IsNullOrWhiteSpace(f.Id) ? null : f.Id;
                    f.Name = string.IsNullOrWhiteSpace(f.Name) ? null : f.Name;
                }
            }

            // Items and nested properties: convert empty scalar strings to null.
            // Do NOT create empty collections/objects here; leaving them null will
            // serialize them as null which preserves the property presence.
            if (vault.Items != null)
            {
                foreach (var it in vault.Items)
                {
                    if (it == null) continue;

                    it.Id = string.IsNullOrWhiteSpace(it.Id) ? null : it.Id;
                    it.OrganizationId = string.IsNullOrWhiteSpace(it.OrganizationId) ? null : it.OrganizationId;
                    it.FolderId = string.IsNullOrWhiteSpace(it.FolderId) ? null : it.FolderId;
                    it.Name = string.IsNullOrWhiteSpace(it.Name) ? null : it.Name;
                    it.Notes = string.IsNullOrWhiteSpace(it.Notes) ? null : it.Notes;

                    // Fields: if present convert inner strings to null, otherwise leave null
                    if (it.Fields != null)
                    {
                        foreach (var fld in it.Fields)
                        {
                            if (fld == null) continue;

                            fld.Name = string.IsNullOrWhiteSpace(fld.Name) ? null : fld.Name;
                            fld.Value = string.IsNullOrWhiteSpace(fld.Value) ? null : fld.Value;
                            // Type and LinkedId remain unchanged
                        }
                    }

                    // Login: if present convert inner strings to null and leave Uris null if missing
                    if (it.Login != null)
                    {
                        it.Login.Username = string.IsNullOrWhiteSpace(it.Login.Username) ? null : it.Login.Username;
                        it.Login.Password = string.IsNullOrWhiteSpace(it.Login.Password) ? null : it.Login.Password;
                        it.Login.Totp = string.IsNullOrWhiteSpace(it.Login.Totp) ? null : it.Login.Totp;

                        if (it.Login.Uris != null)
                        {
                            foreach (var u in it.Login.Uris)
                            {
                                u.Match = string.IsNullOrWhiteSpace(u.Match) ? null : u.Match;
                                u.Uri = string.IsNullOrWhiteSpace(u.Uri) ? null : u.Uri;
                            }
                        }
                    }

                    // CollectionIds: leave as-is (null if missing) so JSON will show "collectionIds": null
                }
            }
        }

        // Public helpers exposed for unit testing and re-use
        /// <summary>
        /// Parse a compact index-set expression into a sorted list of unique indices.
        /// Supports comma-separated values and ranges using a dash (e.g. "0,2,4-6").
        /// Invalid or out-of-range entries are ignored.
        /// </summary>
        /// <param name="input">Index expression to parse.</param>
        /// <param name="maxExclusive">Exclusive upper bound; indices >= maxExclusive are discarded.</param>
        /// <returns>Sorted list of unique indices within the valid range.</returns>
        public static List<int> ParseIndexSet(string input, int maxExclusive)
        {
            var set = new HashSet<int>();

            if (string.IsNullOrWhiteSpace(input)) return set.ToList();

            var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var p in parts)
            {
                var part = p.Trim();

                if (part.Contains('-'))
                {
                    var range = part.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    if (range.Length == 2 && int.TryParse(range[0], out var a) && int.TryParse(range[1], out var b))
                    {
                        var start = Math.Max(0, Math.Min(a, b));
                        var end = Math.Min(maxExclusive - 1, Math.Max(a, b));
                        for (var i = start; i <= end; i++) set.Add(i);
                    }
                }
                else if (int.TryParse(part, out var idx))
                {
                    if (idx >= 0 && idx < maxExclusive) set.Add(idx);
                }
            }

            return set.OrderBy(x => x).ToList();
        }

        /// <summary>
        /// Format an item's details into a multi-line, human-readable string suitable for console output.
        /// Returns an empty string when the provided item is null.
        /// </summary>
        /// <param name="it">Item to format (may be null).</param>
        /// <returns>Trimmed multi-line string describing the item's main properties.</returns>
        public static string FormatItemDetails(Item? it)
        {
            if (it == null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"Id: {it.Id ?? "<null>"}");
            sb.AppendLine($"Name: {it.Name ?? "<null>"}");
            sb.AppendLine($"FolderId: {it.FolderId ?? "<null>"}");
            sb.AppendLine($"Type: {it.Type}");
            sb.AppendLine($"Favorite: {it.Favorite}");
            sb.AppendLine($"Notes: {(string.IsNullOrWhiteSpace(it.Notes) ? "<empty>" : it.Notes)}");
            sb.AppendLine($"Username: {it.Login?.Username ?? "<null>"}");

            var uris = it.Login?.Uris?.Where(u => !string.IsNullOrWhiteSpace(u.Uri)).Select(u => u.Uri).ToList() ??
                       []!;

            if (uris.Count == 0)
                sb.AppendLine("URIs: <none>");
            else
                foreach (var u in uris)
                    sb.AppendLine($"URI: {u}");

            var fields = it.Fields ?? [];
            
            if (fields.Count == 0)
                sb.AppendLine("Fields: <none>");
            else
                foreach (var f in fields)
                    sb.AppendLine($"Field: {f.Name}={f.Value} (type {f.Type})");
            
            return sb.ToString().TrimEnd();
        }
    }
}
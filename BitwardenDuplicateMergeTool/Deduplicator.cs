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

namespace BitwardenDuplicateMergeTool
{
    /// <summary>
    /// Summary statistics produced by a deduplication run.
    /// </summary>
    public class DeduplicationSummary
    {
        /// <summary>
        /// Number of folders in the vault before deduplication.
        /// </summary>
        public int OriginalFolders { get; set; }

        /// <summary>
        /// Number of folders after deduplication.
        /// </summary>
        public int DeduplicatedFolders { get; set; }

        /// <summary>
        /// Number of items in the vault before deduplication.
        /// </summary>
        public int OriginalItems { get; set; }

        /// <summary>
        /// Number of items after deduplication.
        /// </summary>
        public int DeduplicatedItems { get; set; }

        /// <summary>
        /// Computed number of removed folders (OriginalFolders - DeduplicatedFolders).
        /// </summary>
        public int RemovedFolders => OriginalFolders - DeduplicatedFolders;

        /// <summary>
        /// Computed number of removed items (OriginalItems - DeduplicatedItems).
        /// </summary>
        public int RemovedItems => OriginalItems - DeduplicatedItems;
    }

    /// <summary>
    /// Represents a group of items that the deduplicator considers duplicates according to the composite key.
    /// </summary>
    public class DuplicateGroup
    {
        /// <summary>
        /// The composite key used to group duplicates (name|type|username|uris).
        /// </summary>
        public string Key { get; set; } = "";

        /// <summary>
        /// The items that share the composite key. Individual entries may be null and should be guarded by callers.
        /// </summary>
        public List<Item?> Items { get; set; } = [];
    }

    /// <summary>
    /// Core deduplication helpers for Bitwarden exports.
    /// Contains deterministic merge logic and utilities used by the CLI and tests.
    /// </summary>
    public static class Deduplicator
    {
        /// <summary>
        /// Deduplicate the provided vault in-place.
        /// This method will modify the <paramref name="vault"/> instance by removing or merging duplicate folders and items.
        /// </summary>
        /// <param name="vault">The vault to deduplicate. If null, an empty summary is returned.</param>
        /// <param name="mergeMode">
        /// When true, duplicate items are merged using deterministic merge rules (see <see cref="MergeItems"/>).
        /// When false, the item with the latest RevisionDate is kept and older duplicates are discarded.
        /// </param>
        /// <param name="verbose">When true, detailed diagnostics and a visual diff are written to the console.</param>
        /// <returns>
        /// A <see cref="DeduplicationSummary"/> containing counts for original and deduplicated folders/items.
        /// </returns>
        public static DeduplicationSummary Deduplicate(VaultRoot? vault, bool mergeMode = false, bool verbose = false)
        {
            // Capture original lists (shallow copies) so we can produce visual diffs later.
            var originalFolders = vault.Folders != null ? vault.Folders.Select(f => f).ToList() : [];
            var originalItems = vault.Items != null ? vault.Items.Select(i => i).ToList() : [];
    
            var summary = new DeduplicationSummary
            {
                OriginalFolders = originalFolders.Count,
                OriginalItems = originalItems.Count
            };
    
            // Track changes detected during deduplication for verbose visual diffing
            var removedFolders = new List<Folder>();
            var removedItems = new List<Item?>();
            var mergedItems = new List<(Item Existing, Item Incoming, Item Result)>();
            var remappedItems = new List<(Item Item, string OldFolderId, string NewFolderId)>();
    
            // Deduplicate folders by name (case-insensitive)
            // @Todo: Do we need case-sensitive check?
            var uniqueFolders = new Dictionary<string, Folder>(StringComparer.OrdinalIgnoreCase);
            var idMapping = new Dictionary<string, string>();
    
            if (vault.Folders != null)
            {
                foreach (var f in vault.Folders)
                {
                    if (f == null)
                        continue;

                    var folder = f; // local non-null reference
                    var name = folder.Name ?? "";
                    
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        if (!uniqueFolders.ContainsKey(name))
                        {
                            uniqueFolders[name] = folder;
                            
                            if (!string.IsNullOrWhiteSpace(folder.Id))
                                idMapping[folder.Id] = folder.Id;

                            if (verbose)
                                Console.WriteLine($"Keeping folder name='{name}' id='{folder.Id}'");
                        }
                        else
                        {
                            // map duplicate id to the first occurrence id
                            var existing = uniqueFolders[name];

                            // record the duplicate folder for later reporting
                            removedFolders.Add(folder);

                            if (!string.IsNullOrWhiteSpace(folder.Id) && !string.IsNullOrWhiteSpace(existing?.Id))
                            {
                                idMapping[folder.Id] = existing.Id;

                                if (verbose)
                                    Console.WriteLine($"Duplicate folder name='{name}': mapping id '{folder.Id}' -> '{existing.Id}'");
                            }
                            else if (verbose)
                            {
                                // If we cannot remap because of missing ids, still report duplicate
                                Console.WriteLine($"Duplicate folder name='{name}' found (id missing on one side); cannot remap id '{folder.Id ?? "<null>"}'");
                            }
                        }
                    }
                }
            }
    
            vault.Folders = uniqueFolders.Values.ToList();
            summary.DeduplicatedFolders = vault.Folders.Count;

            if (verbose)
                Console.WriteLine($"Folder deduplication: original={summary.OriginalFolders}, deduped={summary.DeduplicatedFolders}, removed={summary.RemovedFolders}");
    
            // Deduplicate items by composite key
            var uniqueItems = new Dictionary<string, Item?>();

            if (vault.Items != null)
            {
                foreach (var item in vault.Items)
                {
                    if (item == null)
                        continue;

                    var it = item!; // local non-null reference
                    var parts = new List<string>
                    {
                        it.Name ?? "",
                        it.Type.ToString()
                    };
    
                    var login = it.Login;

                    if (login != null)
                    {
                        if (!string.IsNullOrWhiteSpace(login.Username))
                            parts.Add(login.Username);

                        if (login.Uris != null)
                        {
                            foreach (var u in login.Uris)
                            {
                                if (!string.IsNullOrWhiteSpace(u?.Uri))
                                    parts.Add(u.Uri);
                            }
                        }
                    }
    
                    var key = string.Join("|", parts);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        if (verbose)
                            Console.WriteLine($"Skipping item id='{it.Id}' because composite key is empty.");

                        continue;
                    }
    
                    if (!uniqueItems.TryGetValue(key, out var existing))
                    {
                        // remap folderId if present
                        var fid = it.FolderId;

                        if (!string.IsNullOrWhiteSpace(fid) && idMapping.TryGetValue(fid, out var mapped))
                        {
                            if (verbose)
                                Console.WriteLine($"Remapping item id='{it.Id}' folderId '{fid}' -> '{mapped}'");

                            // record remapping (old fid -> mapped)
                            remappedItems.Add((it, fid, mapped));
                            it.FolderId = mapped;
                        }
                        uniqueItems[key] = it;

                        if (verbose)
                            Console.WriteLine($"Keeping item key='{key}' id='{it.Id}' name='{it.Name}'");
                    }
                    else
                    {
                        // duplicate found - determine which item is the latest by RevisionDate
                        var existingRev = existing?.RevisionDate ?? DateTime.MinValue;
                        var incomingRev = it.RevisionDate ?? DateTime.MinValue;

                        if (mergeMode)
                        {
                            // Keep the latest as the base, merge the older into it so we preserve as much info as possible.
                            var latest = existing;
                            var older = it;

                            if (incomingRev > existingRev)
                            {
                                latest = it;
                                older = existing;

                                // If the incoming (latest) item has a folderId that needs remapping, apply it.
                                var fid = latest.FolderId;
                                if (!string.IsNullOrWhiteSpace(fid) && idMapping.TryGetValue(fid, out var mapped))
                                {
                                    if (verbose)
                                        Console.WriteLine($"Remapping item id='{latest.Id}' folderId '{fid}' -> '{mapped}'");

                                    remappedItems.Add((latest, fid, mapped));
                                    latest.FolderId = mapped;
                                }
                            }

                            if (verbose)
                                Console.WriteLine($"Merging duplicate item for key='{key}': keeping latest id='{latest.Id}' (rev={latest.RevisionDate}), merging older id='{older.Id}' (rev={older.RevisionDate})");

                            var merged = MergeItems(latest, older);
                            uniqueItems[key] = merged;
                            mergedItems.Add((existing, it, merged));

                            if (verbose)
                                Console.WriteLine($"Merged result id='{uniqueItems[key].Id}' for key='{key}'");
                        }
                        else
                        {
                            // Non-merge mode: keep the item with the latest RevisionDate, discard the older.
                            if (incomingRev > existingRev)
                            {
                                // incoming is newer -> keep incoming
                                var fid = it.FolderId;

                                if (!string.IsNullOrWhiteSpace(fid) && idMapping.TryGetValue(fid, out var mapped))
                                {
                                    if (verbose)
                                        Console.WriteLine($"Remapping item id='{it.Id}' folderId '{fid}' -> '{mapped}'");

                                    remappedItems.Add((it, fid, mapped));
                                    it.FolderId = mapped;
                                }

                                uniqueItems[key] = it;
                                removedItems.Add(existing);

                                if (verbose)
                                    Console.WriteLine($"Duplicate detected for key='{key}': keeping incoming (latest) id='{it.Id}', discarding existing id='{existing.Id}'");
                            }
                            else
                            {
                                // existing is newer or equal -> keep existing, discard incoming
                                removedItems.Add(it);

                                if (verbose)
                                    Console.WriteLine($"Duplicate detected for key='{key}': keeping existing id='{existing.Id}', discarding id='{it.Id}'");
                            }
                        }
                    }
                }
            }
    
            // Order items by priority: RevisionDate (newest first), then CreationDate (newest first).
            // This ensures the most recently revised items are prioritized in the resulting list.
            vault.Items = uniqueItems.Values
                                     .OrderByDescending(i => i.RevisionDate ?? DateTime.MinValue)
                                     .ThenByDescending(i => i.CreationDate ?? DateTime.MinValue)
                                     .ToList();

            summary.DeduplicatedItems = vault.Items.Count;

            if (verbose)
                Console.WriteLine($"Item deduplication: original={summary.OriginalItems}, deduped={summary.DeduplicatedItems}, removed={summary.RemovedItems}");
    
            // Produce a concise visual diff when verbose is enabled using the collected change sets.
            if (verbose)
            {
                try
                {
                    Console.WriteLine();
                    Console.WriteLine("Visual diff of deduplication (removed = red, added/merged = green):");
    
                    // Folders removed
                    if (removedFolders.Count == 0)
                    {
                        Console.WriteLine("  No folder removals");
                    }
                    else
                    {
                        foreach (var f in removedFolders)
                        {
                            var prev = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"- Folder removed: name='{f.Name}' id='{f.Id}'");
                            Console.ForegroundColor = prev;
                        }
                    }
    
                    Console.WriteLine();
    
                    // Items removed (discarded duplicates)
                    if (removedItems.Count == 0)
                    {
                        Console.WriteLine("  No item removals (discarded duplicates)");
                    }
                    else
                    {
                        foreach (var it in removedItems)
                        {
                            var prev = Console.ForegroundColor;

                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"- Item removed: id='{it.Id}' name='{it.Name}' username='{it.Login?.Username}'");
                            Console.ForegroundColor = prev;
                        }
                    }
    
                    Console.WriteLine();
    
                    // Merged items: show a concise, line-oriented visual diff between existing and result
                    if (mergedItems.Count == 0)
                    {
                        Console.WriteLine("  No merged items");
                    }
                    else
                    {
                        foreach (var m in mergedItems)
                        {
                            var prevColor = Console.ForegroundColor;

                            try
                            {
                                Console.WriteLine($"Merged item summary id='{m.Result.Id}':");
                                var existingLines = FormatItemLines(m.Existing);
                                var resultLines = FormatItemLines(m.Result);
    
                                // Use a stable ordering: lines appearing in existing first, then any additional from result.
                                var allLines = new List<string>();

                                foreach (var l in existingLines) if (!allLines.Contains(l)) allLines.Add(l);
                                foreach (var l in resultLines) if (!allLines.Contains(l)) allLines.Add(l);
    
                                foreach (var line in allLines)
                                {
                                    var isInExisting = existingLines.Contains(line);
                                    var isInResult = resultLines.Contains(line);
    
                                    if (isInExisting && !isInResult)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Red;
                                        Console.WriteLine($"- {line}");
                                    }
                                    else if (!isInExisting && isInResult)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Green;
                                        Console.WriteLine($"+ {line}");
                                    }
                                    else
                                    {
                                        Console.ForegroundColor = prevColor;
                                        Console.WriteLine($"  {line}");
                                    }
                                }
    
                                Console.ForegroundColor = prevColor;
                                Console.WriteLine();
                            }
                            finally
                            {
                                Console.ForegroundColor = prevColor;
                            }
                        }
                    }
    
                    // Remapped items (folder id changes)
                    if (remappedItems.Count == 0)
                    {
                        Console.WriteLine("  No item folder remappings detected");
                    }
                    else
                    {
                        foreach (var r in remappedItems)
                        {
                            var prev = Console.ForegroundColor;

                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"- Remapped item (before): id='{r.Item.Id}' folderId='{r.OldFolderId ?? "<null>"}'");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"+ Remapped item (after):  id='{r.Item.Id}' folderId='{r.NewFolderId ?? "<null>"}'");
                            Console.ForegroundColor = prev;
                        }
                    }
    
                    Console.WriteLine();
                }
                catch
                {
                    // Best-effort only; don't throw from reporting
                    Console.WriteLine("Verbose visual diff failed (continuing).");
                }
            }
    
            return summary;
        }

        /// <summary>
        /// Produce a human-readable list of property lines for an item suitable for visual diffs.
        /// The output is intentionally deterministic so it can be compared line-by-line.
        /// </summary>
        /// <param name="item">The item to format. If null, an empty list is returned.</param>
        /// <returns>A list of short descriptive strings representing the item's main properties.</returns>
        static List<string> FormatItemLines(Item item)
        {
            var lines = new List<string>();
 
            if (item == null) return lines;
 
            lines.Add($"Id: {item.Id ?? "<null>"}");
            lines.Add($"Name: {item.Name ?? "<null>"}");
            lines.Add($"FolderId: {item.FolderId ?? "<null>"}");
            lines.Add($"Type: {item.Type}");
            lines.Add($"Favorite: {item.Favorite}");
            lines.Add($"Notes: {(string.IsNullOrWhiteSpace(item.Notes) ? "<empty>" : item.Notes)}");
            lines.Add($"Username: {item.Login?.Username ?? "<null>"}");
 
            // URIs
            var uris = item.Login?.Uris?.Where(u => !string.IsNullOrWhiteSpace(u?.Uri)).Select(u => u.Uri).ToList() ??
                       [];
 
            if (uris.Count == 0)
                lines.Add("URIs: <none>");
            else
            {
                foreach (var u in uris)
                    lines.Add($"URI: {u}");
            }
            // Fields
            var fields = item.Fields ?? [];
 
            if (fields.Count == 0)
                lines.Add("Fields: <none>");
            else
            {
                foreach (var f in fields)
                    lines.Add($"Field: {f.Name}={f.Value} (type {f.Type})");
            }
 
            return lines;
        }
 
        /// <summary>
        /// Merge two items deterministically:
        /// - Keep base item's ID (base is expected to be the latest revision when used by deduplicator)
        /// - Prefer base (latest) non-empty scalar values
        /// - Fill missing scalar/date values from incoming (older) where appropriate
        /// - Combine URIs, fields, collectionIds and passwordHistory uniquely
        /// - Preserve favorites if either is favorite
        /// </summary>
        /// <param name="baseItem">The base item that will be used as the merge target (expected to be the latest).</param>
        /// <param name="incoming">The incoming (older) item whose non-conflicting data will be merged into the base.</param>
        /// <returns>
        /// A new <see cref="Item"/> instance containing the merged result. Returns a clone of <paramref name="baseItem"/>
        /// if <paramref name="incoming"/> is null.
        /// </returns>
        public static Item? MergeItems(Item baseItem, Item? incoming)
        {
            // baseItem is expected to be non-null; if incoming is null just clone base
            if (baseItem == null) throw new ArgumentNullException(nameof(baseItem));
 
            if (incoming == null) return CloneItem(baseItem);
 
            var result = CloneItem(baseItem);
 
            // Name and notes: prefer non-empty (base is latest so prefer its values)
            if (string.IsNullOrWhiteSpace(result.Name) && !string.IsNullOrWhiteSpace(incoming.Name))
                result.Name = incoming.Name;
 
            if (string.IsNullOrWhiteSpace(result.Notes) && !string.IsNullOrWhiteSpace(incoming.Notes))
                result.Notes = incoming.Notes;
 
            result.Favorite = result.Favorite || incoming.Favorite;
 
            // Merge login
            result.Login = MergeLogin(result.Login, incoming.Login);
 
            // Merge fields by (name,type)
            result.Fields = MergeFields(result.Fields, incoming.Fields);
 
            // Merge collectionIds (unique)
            var coll = new List<string>(result.CollectionIds ?? []);
            if (incoming.CollectionIds != null)
            {
                foreach (var cid in incoming.CollectionIds)
                {
                    if (string.IsNullOrWhiteSpace(cid)) continue;
 
                    if (!coll.Any(x => string.Equals(x, cid, StringComparison.OrdinalIgnoreCase)))
                        coll.Add(cid);
                }
            }
            result.CollectionIds = coll.Count == 0 ? null : coll;
 
            // Merge passwordHistory entries (append older entries that aren't duplicates by password+date)
            var ph = new List<PasswordHistoryEntry>(result.PasswordHistory ?? []);
 
            if (incoming.PasswordHistory != null)
            {
                foreach (var p in incoming.PasswordHistory)
                {
                    if (p == null) continue;
 
                    var exists = ph.Any(x => string.Equals(x.Password, p.Password) && x.LastUsedDate == p.LastUsedDate);
 
                    if (!exists)
                        ph.Add(new PasswordHistoryEntry { LastUsedDate = p.LastUsedDate, Password = p.Password });
                }
            }
            result.PasswordHistory = ph.Count == 0 ? null : ph;
 
            // Keep folder mapping if missing on latest
            if (string.IsNullOrWhiteSpace(result.FolderId) && !string.IsNullOrWhiteSpace(incoming.FolderId))
                result.FolderId = incoming.FolderId;
 
            // Dates: baseItem is the latest so prefer its RevisionDate; for other dates, fill from incoming if missing
            if (!result.CreationDate.HasValue && incoming.CreationDate.HasValue)
                result.CreationDate = incoming.CreationDate;
 
            if (!result.DeletedDate.HasValue && incoming.DeletedDate.HasValue)
                result.DeletedDate = incoming.DeletedDate;
 
            if (!result.ArchivedDate.HasValue && incoming.ArchivedDate.HasValue)
                result.ArchivedDate = incoming.ArchivedDate;
 
            return result;
        }

        /// <summary>
        /// Create a deep-ish clone of an item. Collections and nested objects are copied so the clone can be safely mutated.
        /// This intentionally does not attempt to clone every potential nested reference deeply (but copies all primitive values and lists).
        /// </summary>
        /// <param name="src">Source item to clone. Caller must ensure <paramref name="src"/> is not null.</param>
        /// <returns>A new <see cref="Item"/> instance with copied values.</returns>
        static Item? CloneItem(Item src)
        {
            // Cache potentially-null collections/objects to avoid repeated null dereferences
            var login = src.Login;
            var fields = src.Fields;
 
            return new Item
            {
                Id = src.Id,
                OrganizationId = src.OrganizationId,
                FolderId = src.FolderId,
                Type = src.Type,
                Reprompt = src.Reprompt,
                Name = src.Name,
                Notes = src.Notes,
                Favorite = src.Favorite,
                Login = login == null ? null : new Login
                {
                    Password = login.Password,
                    Totp = login.Totp,
                    Username = login.Username,
                    Uris = login.Uris?.Where(u => u != null).Select(u => new UriObj { Match = u.Match, Uri = u.Uri }).ToList() ??
                           []
                },
                Fields = fields?.Where(f => f != null).Select(f => new Field { Name = f.Name, Value = f.Value, Type = f.Type, LinkedId = f.LinkedId }).ToList() ??
                         [],
                CollectionIds = src.CollectionIds == null ? null : src.CollectionIds.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s).ToList(),
                PasswordHistory = src.PasswordHistory == null ? null : src.PasswordHistory.Where(p => p != null).Select(p => new PasswordHistoryEntry { LastUsedDate = p.LastUsedDate, Password = p.Password }).ToList(),
                RevisionDate = src.RevisionDate,
                CreationDate = src.CreationDate,
                DeletedDate = src.DeletedDate,
                ArchivedDate = src.ArchivedDate,
                SecureNote = src.SecureNote == null ? null : new SecureNote { Type = src.SecureNote.Type },
                Card = src.Card == null ? null : new Card
                {
                    CardholderName = src.Card.CardholderName,
                    Brand = src.Card.Brand,
                    Number = src.Card.Number,
                    ExpMonth = src.Card.ExpMonth,
                    ExpYear = src.Card.ExpYear,
                    Code = src.Card.Code
                },
                Identity = src.Identity == null ? null : new Identity
                {
                    Title = src.Identity.Title,
                    FirstName = src.Identity.FirstName,
                    MiddleName = src.Identity.MiddleName,
                    LastName = src.Identity.LastName,
                    Address1 = src.Identity.Address1,
                    Address2 = src.Identity.Address2,
                    Address3 = src.Identity.Address3,
                    City = src.Identity.City,
                    State = src.Identity.State,
                    PostalCode = src.Identity.PostalCode,
                    Country = src.Identity.Country,
                    Company = src.Identity.Company,
                    Email = src.Identity.Email,
                    Phone = src.Identity.Phone,
                    Ssn = src.Identity.Ssn,
                    Username = src.Identity.Username,
                    PassportNumber = src.Identity.PassportNumber,
                    LicenseNumber = src.Identity.LicenseNumber
                }
            };
        }

        /// <summary>
        /// Merge two Login objects preferring non-empty scalar values and combining URIs uniquely (case-insensitive).
        /// </summary>
        /// <param name="a">Primary login (preferred).</param>
        /// <param name="b">Secondary login to take missing values from.</param>
        /// <returns>A merged <see cref="Login"/> instance or null when both inputs are null.</returns>
        static Login? MergeLogin(Login? a, Login? b)
        {
            if (a == null && b == null) return null;
 
            if (a == null) return CloneLogin(b);
 
            if (b == null) return CloneLogin(a);
 
            var merged = CloneLogin(a) ?? new Login();
            if (string.IsNullOrWhiteSpace(merged.Username) && !string.IsNullOrWhiteSpace(b.Username))
                merged.Username = b.Username;
 
            if (string.IsNullOrWhiteSpace(merged.Password) && !string.IsNullOrWhiteSpace(b.Password))
                merged.Password = b.Password;
 
            if (string.IsNullOrWhiteSpace(merged.Totp) && !string.IsNullOrWhiteSpace(b.Totp))
                merged.Totp = b.Totp;
 
            var uris = new List<UriObj>(merged.Uris ?? []);
 
            if (b?.Uris != null)
            {
                foreach (var u in b.Uris)
                {
                    if (u == null) continue;
 
                    if (!uris.Any(x => string.Equals(x.Uri, u.Uri, StringComparison.OrdinalIgnoreCase)))
                        uris.Add(new UriObj { Match = u.Match, Uri = u.Uri });
                }
            }
            merged.Uris = uris;
 
            return merged;
        }

        /// <summary>
        /// Create a shallow clone of a <see cref="Login"/> instance (copies URI list and scalar strings).
        /// </summary>
        /// <param name="src">Source login to clone.</param>
        /// <returns>A cloned <see cref="Login"/> or null when <paramref name="src"/> is null.</returns>
        static Login? CloneLogin(Login? src)
        {
            if (src == null) return null;
 
            return new Login
            {
                Username = src.Username,
                Password = src.Password,
                Totp = src.Totp,
                Uris = src.Uris?.Select(u => new UriObj { Match = u.Match, Uri = u.Uri }).ToList() ?? []
            };
        }

        /// <summary>
        /// Merge two field lists, avoiding duplicates by (name,type) comparison (case-insensitive name).
        /// </summary>
        /// <param name="a">Primary field list (preferred values).</param>
        /// <param name="b">Secondary field list whose non-duplicate entries will be appended.</param>
        /// <returns>A list containing the combined unique fields.</returns>
        static List<Field> MergeFields(List<Field>? a, List<Field>? b)
        {
            var result = new List<Field>();
            if (a != null)
                result.AddRange(a.Select(f => new Field { Name = f.Name, Value = f.Value, Type = f.Type, LinkedId = f.LinkedId }));
 
            if (b != null)
            {
                foreach (var f in b)
                {
                    if (f == null) continue;
 
                    if (!result.Any(r => string.Equals(r.Name, f.Name, StringComparison.OrdinalIgnoreCase) && r.Type == f.Type))
                    {
                        result.Add(new Field { Name = f.Name, Value = f.Value, Type = f.Type, LinkedId = f.LinkedId });
                    }
                }
            }
 
            return result;
        }

        /// <summary>
        /// Find duplicate groups in a vault using the composite key: name | type | username | URIs.
        /// Groups containing more than one item are returned.
        /// </summary>
        /// <param name="vault">The vault to inspect. If null or empty, an empty list is returned.</param>
        /// <returns>A list of <see cref="DuplicateGroup"/> instances where each group contains at least two items.</returns>
        public static List<DuplicateGroup> FindDuplicateGroups(VaultRoot? vault)
        {
            var dict = new Dictionary<string, List<Item>>();
 
            if (vault?.Items != null)
            {
                foreach (var item in vault.Items)
                {
                    if (item == null) continue;
 
                    var parts = new List<string>
                    {
                        item.Name ?? "",
                        item.Type.ToString()
                    };
 
                    var login = item.Login;
                    if (login != null)
                    {
                        if (!string.IsNullOrWhiteSpace(login.Username))
                            parts.Add(login.Username);
 
                        if (login.Uris != null)
                        {
                            foreach (var u in login.Uris)
                            {
                                if (u != null && !string.IsNullOrWhiteSpace(u.Uri))
                                    parts.Add(u.Uri);
                            }
                        }
                    }
 
                    var key = string.Join("|", parts);
 
                    if (string.IsNullOrWhiteSpace(key))
                        continue;
 
                    if (!dict.ContainsKey(key))
                        dict[key] = [];
 
                    dict[key].Add(item);
                }
            }
 
            return dict.Where(kv => kv.Value.Count > 1)
                       .Select(kv => new DuplicateGroup { Key = kv.Key, Items = kv.Value })
                       .ToList();
        }
    }
}
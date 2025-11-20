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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace BitwardenDuplicateMergeTool.Tests
{
    public class DeduplicatorTests
    {
        [Fact]
        public void Deduplicate_SampleVault_NoDuplicates()
        {
            // Locate sample vault file robustly by searching upward for known sample locations.
            // First try to find a 'data/vault.json' sibling folder in the repo,
            // then fall back to 'data/sample.json' if present.
            string? foundPath = null;
            var dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                var candidate1 = Path.GetFullPath(Path.Combine(dir, "data", "vault.json"));
                var candidate2 = Path.GetFullPath(Path.Combine(dir, "data", "sample.json"));
                if (File.Exists(candidate1))
                {
                    foundPath = candidate1;
                    break;
                }
                if (File.Exists(candidate2))
                {
                    foundPath = candidate2;
                    break;
                }
                var parent = Directory.GetParent(dir);
                dir = parent?.FullName;
            }
    
            Assert.False(string.IsNullOrWhiteSpace(foundPath), $"Sample vault file not found from {AppContext.BaseDirectory}. Looked for data/vault.json and data/sample.json upwards from the test directory.");
            var path = foundPath!;

            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var vault = JsonSerializer.Deserialize<VaultRoot>(json, options);
            Assert.NotNull(vault);

            var originalFolders = vault.Folders?.Count ?? 0;
            var originalItems = vault.Items?.Count ?? 0;

            var summary = Deduplicator.Deduplicate(vault);

            Assert.Equal(originalFolders, summary.DeduplicatedFolders);
            Assert.Equal(originalItems, summary.DeduplicatedItems);
            Assert.Equal(0, summary.RemovedFolders);
            Assert.Equal(0, summary.RemovedItems);
        }

        [Fact]
        public void MergeItems_PrefersNonEmptyAndCombinesCollections()
        {
            var baseItem = CreateItem(
                id: "base-id",
                name: "Base Site",
                notes: null,
                favorite: false,
                username: "baseuser",
                password: "basepass",
                totp: null,
                uri: "https://a.example.com",
                fields: [new Field { Name = "x", Value = "1", Type = 0 }],
                folderId: null
            );
 
            var incoming = new Item
            {
                Id = "incoming-id",
                Name = null,
                Notes = "Some notes",
                Favorite = true,
                FolderId = "folder-123",
                Fields =
                [
                    new Field { Name = "y", Value = "2", Type = 0 },
                    new Field { Name = "X", Value = "should-be-ignored", Type = 0 }
                ],
                Login = new Login
                {
                    Username = null,
                    Password = "incomingpass",
                    Totp = "TOTPVALUE",
                    Uris =
                    [
                        new UriObj { Uri = "https://b.example.com" },
                        new UriObj { Uri = "https://A.example.com" }
                    ]
                }
            };

            var merged = Deduplicator.MergeItems(baseItem, incoming);

            // Id must remain from base
            Assert.Equal("base-id", merged.Id);

            // Name preserved from base
            Assert.Equal("Base Site", merged.Name);

            // Notes came from incoming because base was null
            Assert.Equal("Some notes", merged.Notes);

            // Favorite preserved if either is favorite
            Assert.True(merged.Favorite);

            // Login: username from base, password from base (base has non-empty)
            Assert.Equal("baseuser", merged.Login.Username);
            Assert.Equal("basepass", merged.Login.Password);

            // Totp should be filled from incoming as base had none
            Assert.Equal("TOTPVALUE", merged.Login.Totp);

            // URIs combined uniquely (case-insensitive)
            var uris = merged.Login.Uris.Select(u => u.Uri).OrderBy(u => u).ToList();
            Assert.Contains("https://a.example.com", uris);
            Assert.Contains("https://b.example.com", uris);
            Assert.Equal(2, uris.Count);

            // Fields combined uniquely by (name,type) case-insensitive
            Assert.NotNull(merged.Fields);
            Assert.Equal(2, merged.Fields.Count);
            Assert.Contains(merged.Fields, f => string.Equals(f.Name, "x", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(merged.Fields, f => string.Equals(f.Name, "y", StringComparison.OrdinalIgnoreCase));

            // FolderId should be taken from incoming since base had none
            Assert.Equal("folder-123", merged.FolderId);
        }

        [Fact]
        public void MergeFields_AvoidsDuplicatesCaseInsensitive()
        {
            var a = new List<Field>
            {
                new Field { Name = "Email", Value = "a@x.com", Type = 0 }
            };
            var b = new List<Field>
            {
                new Field { Name = "email", Value = "b@x.com", Type = 0 }, // should be considered duplicate
                new Field { Name = "Phone", Value = "123", Type = 0 }
            };

            var merged = typeof(Deduplicator)
                .GetMethod("MergeFields", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, [a, b]) as List<Field>;

            Assert.NotNull(merged);
            Assert.Equal(2, merged.Count);
            Assert.Contains(merged, f => string.Equals(f.Name, "Email", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(merged, f => string.Equals(f.Name, "Phone", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void MergeLogin_MergesUrisUniquelyAndPrefersNonEmptyScalars()
        {
            var a = new Login
            {
                Username = "userA",
                Password = "",
                Totp = null,
                Uris = [new UriObj { Uri = "https://one.com" }]
            };

            var b = new Login
            {
                Username = null,
                Password = "pwdB",
                Totp = "TOTP",
                Uris = [new UriObj { Uri = "https://two.com" }, new UriObj { Uri = "https://ONE.com" }]
            };

            var merged = typeof(Deduplicator)
                .GetMethod("MergeLogin", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, [a, b]) as Login;

            Assert.NotNull(merged);
            Assert.Equal("userA", merged.Username);
            Assert.Equal("pwdB", merged.Password);
            Assert.Equal("TOTP", merged.Totp);
            Assert.Equal(2, merged.Uris.Count);
            var uris = merged.Uris.Select(u => u.Uri).OrderBy(u => u).ToList();
            Assert.Contains("https://one.com", uris);
            Assert.Contains("https://two.com", uris);
        }

        [Fact]
        public void FindDuplicateGroups_GroupsByCompositeKey()
        {
            var vault = new VaultRoot
            {
                Items =
                [
                    CreateItem("1", "Site", username: "u1", uri: "https://a"),
                    CreateItem("2", "Site", username: "u1", uri: "https://a"),
                    CreateItem("3", "Other", username: "u2", uri: "https://b")
                ]
            };

            var groups = Deduplicator.FindDuplicateGroups(vault);
            Assert.Single(groups);
            var g = groups.First();
            Assert.Equal(2, g.Items.Count);
            Assert.Contains(g.Items, it => it.Id == "1");
            Assert.Contains(g.Items, it => it.Id == "2");
        }

        [Fact]
        public void Interactive_Action_Merge_ReplacesFirstAndRemovesOthers()
        {
            var groupItems = new List<Item>
            {
                CreateItem("1", "G", username: "u", uri: "https://a", favorite: false),
                CreateItem("2", "G", username: "u", uri: "https://b", favorite: true),
                CreateItem("3", "G", username: "u", uri: "https://c", favorite: false)
            };

            var itemsList = new List<Item>
            {
                groupItems[0],
                groupItems[1],
                groupItems[2],
                CreateItem("x", "Other", username: "o", uri: "https://other")
            };

            // Simulate 'm' action from Program: merge via aggregate, replace first, remove others
            var merged = groupItems.Aggregate((a, b) => Deduplicator.MergeItems(a, b));
            var firstId = groupItems.First().Id;
            var idx = itemsList.FindIndex(x => x.Id == firstId);
            if (idx >= 0)
                itemsList[idx] = merged;

            var removeIds = groupItems.Skip(1).Select(x => x.Id).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet();
            itemsList = itemsList.Where(x => !removeIds.Contains(x.Id)).ToList();

            Assert.Equal(2, itemsList.Count); // merged group (1) + "Other"
            Assert.Contains(itemsList, i => i.Id == "1");
            var kept = itemsList.First(i => i.Id == "1");
            Assert.True(kept.Favorite); // merged favorite should be true because one of inputs was favorite
        }

        [Fact]
        public void Interactive_Action_KeepFirst_RemovesOthers()
        {
            var groupItems = new List<Item>
            {
                CreateItem("1", "G", username: "u", uri: "https://a"),
                CreateItem("2", "G", username: "u", uri: "https://b")
            };

            var itemsList = new List<Item>
            {
                groupItems[0],
                groupItems[1]
            };

            // Simulate 'k' (keep-first)
            var removeIds = groupItems.Skip(1).Select(x => x.Id).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet();
            itemsList = itemsList.Where(x => !removeIds.Contains(x.Id)).ToList();

            Assert.Single(itemsList);
            Assert.Equal("1", itemsList[0].Id);
        }

        [Fact]
        public void Interactive_Action_Skip_LeavesItemsUnchanged()
        {
            var groupItems = new List<Item>
            {
                CreateItem("1", "G", username: "u", uri: "https://a"),
                CreateItem("2", "G", username: "u", uri: "https://b")
            };

            var itemsList = new List<Item>
            {
                groupItems[0],
                groupItems[1]
            };

            // Simulate 's' (skip) -> no changes
            // No-op

            Assert.Equal(2, itemsList.Count);
            Assert.Contains(itemsList, i => i.Id == "1");
            Assert.Contains(itemsList, i => i.Id == "2");
        }

        // Helper to create items quickly for tests
        static Item CreateItem(
            string id,
            string name,
            int type = 1,
            string username = null,
            string password = null,
            string totp = null,
            string uri = null,
            bool favorite = false,
            string notes = null,
            string folderId = null,
            List<Field> fields = null)
        {
            var item = new Item
            {
                Id = id,
                Name = name,
                Type = type,
                Notes = notes,
                Favorite = favorite,
                FolderId = folderId,
                Fields = fields
            };

            if (username != null || password != null || totp != null || uri != null)
            {
                item.Login = new Login
                {
                    Username = username,
                    Password = password,
                    Totp = totp,
                    Uris = uri != null ? [new UriObj { Uri = uri, Match = "" }] : null
                };
            }

            return item;
        }

        // Folder deduplication tests
        [Fact]
        public void Deduplicate_Folders_CaseInsensitiveAndRemapFolderIds()
        {
            var vault = new VaultRoot
            {
                Folders =
                [
                    new Folder { Id = "f1", Name = "Work" },
                    new Folder { Id = "f2", Name = "work" }
                ],
                Items =
                [
                    CreateItem("i1", "Site A", folderId: "f2"),
                    CreateItem("i2", "Site B", folderId: "f1")
                ]
            };

            var summary = Deduplicator.Deduplicate(vault);

            // Folders: original 2, deduplicated 1, removed 1
            Assert.Equal(2, summary.OriginalFolders);
            Assert.Equal(1, summary.DeduplicatedFolders);
            Assert.Equal(1, summary.RemovedFolders);

            // Items should remain intact count-wise
            Assert.Equal(2, summary.OriginalItems);
            Assert.Equal(2, summary.DeduplicatedItems);

            // The item that referenced the duplicate folder id should be remapped to the first folder id ("f1")
            var mapped = vault.Items.First(it => it.Id == "i1");
            Assert.Equal("f1", mapped.FolderId);
        }

        [Fact]
        public void Deduplicate_Folders_DuplicateNameWithMissingFirstId_DoesNotRemap()
        {
            // First folder has no id, second folder has an id; mapping should not occur because first id is missing
            var vault = new VaultRoot
            {
                Folders =
                [
                    new Folder { Id = null, Name = "Personal" },
                    new Folder { Id = "f2", Name = "personal" }
                ],
                Items = [CreateItem("i1", "Site A", folderId: "f2")]
            };

            var summary = Deduplicator.Deduplicate(vault);

            // Folders deduplicated (name-based), but since first had no id, id remapping should not have been created.
            Assert.Equal(2, summary.OriginalFolders);
            Assert.Equal(1, summary.DeduplicatedFolders);

            // The item that referenced "f2" should keep its FolderId because no remapping was possible
            var item = vault.Items.First(it => it.Id == "i1");
            Assert.Equal("f2", item.FolderId);
        }
    }
}
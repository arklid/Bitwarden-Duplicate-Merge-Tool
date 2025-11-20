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
using System.Linq;
using System.Reflection;
using Xunit;

namespace BitwardenDuplicateMergeTool.Tests
{
    public class AdditionalTests
    {
        [Fact]
        public void Deduplicate_WithMergeMode_MergesDuplicateItemsIntoOne()
        {
            // Two items that will produce the same composite key (same name/type/username/uri)
            var a = CreateItem("a", "Site", username: "u", uri: "https://a", favorite: false);
            a.RevisionDate = new DateTime(2020, 1, 1);

            var b = CreateItem("b", "Site", username: "u", uri: "https://a", favorite: true);
            b.RevisionDate = new DateTime(2021, 1, 1);

            var vault = new VaultRoot
            {
                Folders = [],
                Items = [a, b]
            };

            var summary = Deduplicator.Deduplicate(vault, mergeMode: true, verbose: false);

            // Should merge to a single item
            Assert.Equal(2, summary.OriginalItems);
            Assert.Equal(1, summary.DeduplicatedItems);
            Assert.Equal(1, summary.RemovedItems);

            Assert.NotNull(vault.Items);
            Assert.Single(vault.Items);

            var merged = vault.Items.First();
            // Id should be preserved from the latest (b) according to Deduplicate merge selection
            Assert.Equal(b.Id, merged.Id ?? b.Id);
            // Favorite should be true because one input was favorite
            Assert.True(merged.Favorite);
        }

        [Fact]
        public void MergeItems_PreservesAndCombinesPasswordHistoryAndCollectionIds()
        {
            var baseItem = CreateItem("base", "Site", username: "u", uri: "https://one");
            baseItem.PasswordHistory =
                [new PasswordHistoryEntry { LastUsedDate = new DateTime(2019, 1, 1), Password = "old" }];
            baseItem.CollectionIds = ["c1"];

            var incoming = CreateItem("inc", "Site", username: "u", uri: "https://two");
            incoming.PasswordHistory =
            [
                new PasswordHistoryEntry { LastUsedDate = new DateTime(2020, 1, 1), Password = "new" },
                // duplicate entry that should be detected as duplicate if identical
                new PasswordHistoryEntry { LastUsedDate = new DateTime(2019, 1, 1), Password = "old" }
            ];
            incoming.CollectionIds = ["C1", "c2"]; // include a case-variant duplicate

            var merged = Deduplicator.MergeItems(baseItem, incoming);

            Assert.NotNull(merged.PasswordHistory);
            // Should combine unique entries (old + new) -> 2 unique
            Assert.Equal(2, merged.PasswordHistory.Count);
            Assert.Contains(merged.PasswordHistory, p => p.Password == "old");
            Assert.Contains(merged.PasswordHistory, p => p.Password == "new");

            // CollectionIds should be combined uniquely (case-insensitive)
            Assert.NotNull(merged.CollectionIds);
            Assert.Contains(merged.CollectionIds, cid => string.Equals(cid, "c1", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(merged.CollectionIds, cid => string.Equals(cid, "c2", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void EnsureNonNullProperties_ConvertsEmptyStringsToNull()
        {
            // Build vault with empty strings and nested empty strings
            var vault = new VaultRoot
            {
                Folders = [new Folder { Id = "", Name = "" }],
                Items =
                [
                    new Item
                    {
                        Id = "",
                        OrganizationId = "",
                        FolderId = "",
                        Name = "",
                        Notes = "",
                        Fields = [new Field { Name = "", Value = "" }],
                        Login = new Login
                        {
                            Username = "",
                            Password = "",
                            Totp = "",
                            Uris = [new UriObj { Match = "", Uri = "" }]
                        },
                        CollectionIds = null
                    }
                ]
            };

            // Call private static Program.EnsureNonNullProperties via reflection
            var method = typeof(Program).GetMethod("EnsureNonNullProperties", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(null, [vault]);

            // After invocation empty strings should be converted to null
            Assert.NotNull(vault.Folders);
            var f = vault.Folders.First();
            Assert.Null(f.Id);
            Assert.Null(f.Name);

            var it = vault.Items.First();
            Assert.Null(it.Id);
            Assert.Null(it.OrganizationId);
            Assert.Null(it.FolderId);
            Assert.Null(it.Name);
            Assert.Null(it.Notes);

            Assert.NotNull(it.Fields);
            var fld = it.Fields.First();
            Assert.Null(fld.Name);
            Assert.Null(fld.Value);

            Assert.NotNull(it.Login);
            Assert.Null(it.Login.Username);
            Assert.Null(it.Login.Password);
            Assert.Null(it.Login.Totp);

            Assert.NotNull(it.Login.Uris);
            var uri = it.Login.Uris.First();
            Assert.Null(uri.Match);
            Assert.Null(uri.Uri);
        }

        // Helper to create items quickly for tests (mirrors the helper used elsewhere)
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
    }
}
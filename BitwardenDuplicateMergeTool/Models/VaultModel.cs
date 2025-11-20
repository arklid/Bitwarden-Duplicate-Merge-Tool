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

using System.Text.Json.Serialization;

namespace BitwardenDuplicateMergeTool.Models
{
    // Models mapping the Bitwarden export schema
    /// <summary>
    /// Root model representing a Bitwarden exported vault.
    /// Contains top-level metadata and the collections, folders and items exported.
    /// Properties are nullable to reflect fields that may be absent in the export.
    /// </summary>
    public class VaultRoot
    {
        /// <summary>
        /// True if the exported vault content is encrypted.
        /// </summary>
        [JsonPropertyName("encrypted")]
        public bool Encrypted { get; set; }
 
        /// <summary>
        /// Optional list of collections included in the export.
        /// May be null when the export does not include collections.
        /// </summary>
        [JsonPropertyName("collections")]
        public List<Collection>? Collections { get; set; }
 
        /// <summary>
        /// Optional list of folders included in the export.
        /// May be null when no folders were exported.
        /// </summary>
        [JsonPropertyName("folders")]
        public List<Folder>? Folders { get; set; }
 
        /// <summary>
        /// Optional list of items contained in the vault.
        /// Individual list entries may be null and should be guarded when enumerating.
        /// </summary>
        [JsonPropertyName("items")]
        public List<Item?>? Items { get; set; }
    }

    /// <summary>
    /// Represents a folder in the Bitwarden export.
    /// </summary>
    public class Folder
    {
        /// <summary>
        /// The folder identifier (may be null or empty in some exports).
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }
 
        /// <summary>
        /// The human-readable folder name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// Represents an item exported from Bitwarden (login, secure note, card, identity, etc.).
    /// The model mirrors the Bitwarden export schema and uses nullable members for optional data.
    /// </summary>
    public class Item
    {
        /// <summary>
        /// The unique identifier of the item.
        /// </summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }
 
        /// <summary>
        /// Identifier of the organization this item belongs to (if any).
        /// </summary>
        [JsonPropertyName("organizationId")]
        public string? OrganizationId { get; set; }
 
        /// <summary>
        /// Identifier of the folder containing the item (may be null).
        /// </summary>
        [JsonPropertyName("folderId")]
        public string? FolderId { get; set; }
 
        /// <summary>
        /// Numeric type code describing the item kind (login, card, identity, secure note, ...).
        /// </summary>
        [JsonPropertyName("type")]
        public int Type { get; set; }
 
        /// <summary>
        /// Reprompt setting (numeric), left as-is from the export.
        /// </summary>
        [JsonPropertyName("reprompt")]
        public int Reprompt { get; set; }
 
        /// <summary>
        /// Human-readable name/title of the item.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
 
        /// <summary>
        /// Notes stored with the item.
        /// </summary>
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
 
        /// <summary>
        /// Whether the item is marked as a favorite.
        /// </summary>
        [JsonPropertyName("favorite")]
        public bool Favorite { get; set; }
 
        /// <summary>
        /// Login-specific data (username, password, URIs, etc.) when applicable.
        /// </summary>
        [JsonPropertyName("login")]
        public Login? Login { get; set; }
 
        /// <summary>
        /// Custom fields associated with the item.
        /// </summary>
        [JsonPropertyName("fields")]
        public List<Field>? Fields { get; set; }
 
        /// <summary>
        /// Collection IDs that this item belongs to. May be null when not present.
        /// </summary>
        [JsonPropertyName("collectionIds")]
        public List<string>? CollectionIds { get; set; }
 
        /// <summary>
        /// Historical passwords kept for the item (if any).
        /// </summary>
        [JsonPropertyName("passwordHistory")]
        public List<PasswordHistoryEntry>? PasswordHistory { get; set; }
 
        /// <summary>
        /// Timestamp of last revision (nullable).
        /// </summary>
        [JsonPropertyName("revisionDate")]
        public DateTime? RevisionDate { get; set; }
 
        /// <summary>
        /// Timestamp of creation (nullable).
        /// </summary>
        [JsonPropertyName("creationDate")]
        public DateTime? CreationDate { get; set; }
 
        /// <summary>
        /// Timestamp when the item was deleted (nullable).
        /// </summary>
        [JsonPropertyName("deletedDate")]
        public DateTime? DeletedDate { get; set; }
 
        /// <summary>
        /// Timestamp when the item was archived (nullable).
        /// </summary>
        [JsonPropertyName("archivedDate")]
        public DateTime? ArchivedDate { get; set; }
 
        /// <summary>
        /// Secure note payload (when type indicates secure note).
        /// </summary>
        [JsonPropertyName("secureNote")]
        public SecureNote? SecureNote { get; set; }
 
        /// <summary>
        /// Card details (when type indicates a card).
        /// </summary>
        [JsonPropertyName("card")]
        public Card? Card { get; set; }
 
        /// <summary>
        /// Identity details (when type indicates an identity).
        /// </summary>
        [JsonPropertyName("identity")]
        public Identity? Identity { get; set; }
    }

    /// <summary>
    /// Login payload for an Item (username, password, TOTP and associated URIs).
    /// </summary>
    public class Login
    {
        /// <summary>
        /// List of URIs associated with the login (may be null).
        /// </summary>
        [JsonPropertyName("uris")]
        public List<UriObj>? Uris { get; set; }
 
        /// <summary>
        /// Username for the login (may be null).
        /// </summary>
        [JsonPropertyName("username")]
        public string? Username { get; set; }
 
        /// <summary>
        /// Password for the login (may be null).
        /// </summary>
        [JsonPropertyName("password")]
        public string? Password { get; set; }
 
        /// <summary>
        /// Time-based one-time password seed (may be null).
        /// </summary>
        [JsonPropertyName("totp")]
        public string? Totp { get; set; }
    }

    /// <summary>
    /// Represents a URI entry for a login, including match rules and the URI string.
    /// </summary>
    public class UriObj
    {
        /// <summary>
        /// Matching rule or host pattern used by Bitwarden (may be null).
        /// </summary>
        [JsonPropertyName("match")]
        public string? Match { get; set; }
 
        /// <summary>
        /// The URI value (may be null).
        /// </summary>
        [JsonPropertyName("uri")]
        public string? Uri { get; set; }
    }
 
    /// <summary>
    /// Represents a custom field attached to an item.
    /// </summary>
    public class Field
    {
        /// <summary>
        /// Field name (may be null).
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
 
        /// <summary>
        /// Field value (may be null).
        /// </summary>
        [JsonPropertyName("value")]
        public string? Value { get; set; }
 
        /// <summary>
        /// Numeric type code for the field.
        /// </summary>
        [JsonPropertyName("type")]
        public int Type { get; set; }
 
        /// <summary>
        /// Optional linked id (nullable).
        /// </summary>
        [JsonPropertyName("linkedId")]
        public int? LinkedId { get; set; }
    }

    /// <summary>
    /// Secure note metadata.
    /// </summary>
    public class SecureNote
    {
        /// <summary>
        /// Numeric secure-note type code.
        /// </summary>
        [JsonPropertyName("type")]
        public int Type { get; set; }
    }

    /// <summary>
    /// Credit/debit card details stored on an item.
    /// </summary>
    public class Card
    {
        /// <summary>Cardholder name (may be null).</summary>
        [JsonPropertyName("cardholderName")]
        public string? CardholderName { get; set; }
 
        /// <summary>Card brand (e.g. Visa, MasterCard) (may be null).</summary>
        [JsonPropertyName("brand")]
        public string? Brand { get; set; }
 
        /// <summary>Card number (may be null).</summary>
        [JsonPropertyName("number")]
        public string? Number { get; set; }
 
        /// <summary>Expiry month as string (may be null).</summary>
        [JsonPropertyName("expMonth")]
        public string? ExpMonth { get; set; }
 
        /// <summary>Expiry year as string (may be null).</summary>
        [JsonPropertyName("expYear")]
        public string? ExpYear { get; set; }
 
        /// <summary>Security code (may be null).</summary>
        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }

    /// <summary>
    /// Identity details stored on an item (name, address and other identity fields).
    /// </summary>
    public class Identity
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }
 
        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }
 
        [JsonPropertyName("middleName")]
        public string? MiddleName { get; set; }
 
        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }
 
        [JsonPropertyName("address1")]
        public string? Address1 { get; set; }
 
        [JsonPropertyName("address2")]
        public string? Address2 { get; set; }
 
        [JsonPropertyName("address3")]
        public string? Address3 { get; set; }
 
        [JsonPropertyName("city")]
        public string? City { get; set; }
 
        [JsonPropertyName("state")]
        public string? State { get; set; }
 
        [JsonPropertyName("postalCode")]
        public string? PostalCode { get; set; }
 
        [JsonPropertyName("country")]
        public string? Country { get; set; }
 
        [JsonPropertyName("company")]
        public string? Company { get; set; }
 
        [JsonPropertyName("email")]
        public string? Email { get; set; }
 
        [JsonPropertyName("phone")]
        public string? Phone { get; set; }
 
        [JsonPropertyName("ssn")]
        public string? Ssn { get; set; }
 
        [JsonPropertyName("username")]
        public string? Username { get; set; }
 
        [JsonPropertyName("passportNumber")]
        public string? PassportNumber { get; set; }
 
        [JsonPropertyName("licenseNumber")]
        public string? LicenseNumber { get; set; }
    }

    /// <summary>
    /// Represents a collection in a Bitwarden export.
    /// </summary>
    public class Collection
    {
        /// <summary>The collection identifier.</summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }
 
        /// <summary>The organization id the collection belongs to (nullable).</summary>
        [JsonPropertyName("organizationId")]
        public string? OrganizationId { get; set; }
 
        /// <summary>Collection display name (nullable).</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
 
        /// <summary>External identifier for the collection (nullable).</summary>
        [JsonPropertyName("externalId")]
        public string? ExternalId { get; set; }
    }

    /// <summary>
    /// Entry describing a historical password for an item.
    /// </summary>
    public class PasswordHistoryEntry
    {
        /// <summary>
        /// When the password was last used (nullable).
        /// </summary>
        [JsonPropertyName("lastUsedDate")]
        public DateTime? LastUsedDate { get; set; }
 
        /// <summary>
        /// The password value (nullable). Stored here only if present in the export.
        /// </summary>
        [JsonPropertyName("password")]
        public string? Password { get; set; }
    }
}
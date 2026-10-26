// External Steam boundary for executable publishing tests. No Steam client or
// network is used; the real PowerShell scripts prepare and submit each update.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Text.Json;

namespace Steamworks
{
    public enum EResult { k_EResultOK = 1, k_EResultFail = 2, k_EResultFileNotFound = 9 }
    public enum EWorkshopFileType { k_EWorkshopFileTypeCommunity }
    public enum ERemoteStoragePublishedFileVisibility { k_ERemoteStoragePublishedFileVisibilityPublic }
    public struct AppId_t { public uint m_AppId; public AppId_t(uint id) { m_AppId = id; } }
    public struct PublishedFileId_t
    {
        public ulong m_PublishedFileId;
        public PublishedFileId_t(ulong id) { m_PublishedFileId = id; }
        public override string ToString() { return m_PublishedFileId.ToString(); }
    }
    public struct UGCUpdateHandle_t { public ulong m_UGCUpdateHandle; }
    public struct SteamAPICall_t { public ulong m_SteamAPICall; }
    [StructLayout(LayoutKind.Sequential)]
    public struct SubmitItemUpdateResult_t
    {
        public const int k_iCallback = 3404;
        public EResult m_eResult;
        [MarshalAs(UnmanagedType.I1)] public bool m_bUserNeedsToAcceptWorkshopLegalAgreement;
        public PublishedFileId_t m_nPublishedFileId;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct CreateItemResult_t
    {
        public const int k_iCallback = 3403;
        public EResult m_eResult;
        public PublishedFileId_t m_nPublishedFileId;
        [MarshalAs(UnmanagedType.I1)] public bool m_bUserNeedsToAcceptWorkshopLegalAgreement;
    }
    public sealed class Upload
    {
        public ulong Item;
        public string Content, Title, Description, Preview, ChangeNote;
        public string[] Tags;
        public bool Public;
    }
    public static class Fixture
    {
        public static bool Online = true, NeedsAgreement, RejectTags, ResultConsumed;
        public static int Initializations, Shutdowns, Creates, Starts, CompletionChecks;
        public static EResult Result = EResult.k_EResultOK;
        public static Upload Pending, Published;
        public static readonly List<Upload> Submissions = new List<Upload>();
        public static void Configure()
        {
            using (var input = JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("WORKSHOP_TEST_INPUT"))))
            {
                var settings = input.RootElement;
                Online = settings.GetProperty("Online").GetBoolean();
                NeedsAgreement = settings.GetProperty("NeedsAgreement").GetBoolean();
                RejectTags = settings.GetProperty("RejectTags").GetBoolean();
                Result = (EResult)settings.GetProperty("Result").GetInt32();
            }
        }
        public static void Report()
        {
            File.WriteAllText(Environment.GetEnvironmentVariable("WORKSHOP_TEST_OUTPUT"), JsonSerializer.Serialize(new {
                ProcessId = Environment.ProcessId, Initializations, Shutdowns, Creates, Starts, Published, Submissions
            }, new JsonSerializerOptions { IncludeFields = true }));
        }
    }
    public static class SteamAPI
    {
        public static bool Init() { Fixture.Configure(); Fixture.Initializations++; return true; }
        public static void Shutdown() { Fixture.Shutdowns++; Fixture.Report(); }
        // Steamworks.NET's manual dispatcher consumes even unregistered call
        // results. Confirmed with a real read-only CheckFileSignature request.
        public static void RunCallbacks() { Fixture.ResultConsumed = true; }
    }
    public static class SteamUser { public static bool BLoggedOn() { return Fixture.Online; } }
    public static class SteamUtils
    {
        public static AppId_t GetAppID() { return new AppId_t(294100); }
        // Force a wait iteration so a result arriving between polls is covered.
        public static bool IsAPICallCompleted(SteamAPICall_t call, out bool failed)
        { failed = false; return ++Fixture.CompletionChecks > 1; }
        public static bool GetAPICallResult(SteamAPICall_t call, IntPtr buffer, int size, int callback, out bool failed)
        {
            failed = Fixture.ResultConsumed;
            if (failed) return false;
            if (callback == CreateItemResult_t.k_iCallback)
                Marshal.StructureToPtr(new CreateItemResult_t {
                    m_eResult = Fixture.Result, m_nPublishedFileId = new PublishedFileId_t(9876543210),
                    m_bUserNeedsToAcceptWorkshopLegalAgreement = Fixture.NeedsAgreement
                }, buffer, false);
            else if (callback == SubmitItemUpdateResult_t.k_iCallback)
                Marshal.StructureToPtr(new SubmitItemUpdateResult_t {
                    m_eResult = Fixture.Online ? Fixture.Result : EResult.k_EResultFileNotFound,
                    m_nPublishedFileId = new PublishedFileId_t(Fixture.Pending.Item),
                    m_bUserNeedsToAcceptWorkshopLegalAgreement = Fixture.NeedsAgreement
                }, buffer, false);
            else throw new InvalidOperationException("Unexpected callback: " + callback);
            return true;
        }
    }
    public static class SteamUGC
    {
        public static SteamAPICall_t CreateItem(AppId_t app, EWorkshopFileType type)
        { Fixture.Creates++; Fixture.CompletionChecks = 0; return new SteamAPICall_t { m_SteamAPICall = 1 }; }
        public static UGCUpdateHandle_t StartItemUpdate(AppId_t app, PublishedFileId_t item)
        {
            if (app.m_AppId != 294100) throw new InvalidOperationException("Wrong app");
            Fixture.Starts++;
            Fixture.Pending = new Upload { Item = item.m_PublishedFileId };
            return new UGCUpdateHandle_t { m_UGCUpdateHandle = 42 };
        }
        public static bool SetItemTags(UGCUpdateHandle_t h, IList<string> tags)
        { Fixture.Pending.Tags = new List<string>(tags).ToArray(); return !Fixture.RejectTags; }
        public static bool SetItemContent(UGCUpdateHandle_t h, string s) { Fixture.Pending.Content = s; return true; }
        public static bool SetItemTitle(UGCUpdateHandle_t h, string s) { Fixture.Pending.Title = s; return true; }
        public static bool SetItemDescription(UGCUpdateHandle_t h, string s) { Fixture.Pending.Description = s; return true; }
        public static bool SetItemPreview(UGCUpdateHandle_t h, string s) { Fixture.Pending.Preview = s; return true; }
        public static bool SetItemVisibility(UGCUpdateHandle_t h, ERemoteStoragePublishedFileVisibility v)
        { Fixture.Pending.Public = true; return true; }
        public static SteamAPICall_t SubmitItemUpdate(UGCUpdateHandle_t h, string note)
        {
            Fixture.Pending.ChangeNote = note;
            Fixture.CompletionChecks = 0;
            Fixture.Submissions.Add(Fixture.Pending);
            if (Fixture.Online && Fixture.Result == EResult.k_EResultOK) Fixture.Published = Fixture.Pending;
            return new SteamAPICall_t { m_SteamAPICall = 2 };
        }
    }
}

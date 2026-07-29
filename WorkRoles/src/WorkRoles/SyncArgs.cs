using System.Collections.Generic;

namespace WorkRoles
{
    /// Single synced arg for ApplyImport: MP's invoker breaks on wide
    /// signatures, and a class with a SyncWorker stays extensible.
    public class ImportSelection
    {
        public string xml;
        public bool palette;
        public bool paletteOverwrite;
        public bool roles;
        public bool rolesOverwrite;
        public bool paths;
        public bool pathsOverwrite;
        public bool order;
        public List<int> paletteRows = new List<int>();
        public List<int> roleRows = new List<int>();
        public List<int> pathRows = new List<int>();
    }

    /// Single synced arg for CommitScaleEdit: scale rows travel as codec
    /// strings; null rows keep the target's existing values.
    public class ScaleEdit
    {
        public int roleId = -1;   // role to point at the target scale; -1 = none
        public string sourceName; // fork/clone source when the target is new
        public string targetName; // scale receiving the values
        public string min;
        public string train;
        public string max;
    }

    /// Single synced arg for RestoreSelected: MP's invoker breaks on wide
    /// signatures, and a class with a SyncWorker stays extensible.
    public class RestoreSelection
    {
        public List<string> templateDefs = new List<string>();
        public List<string> workTypes = new List<string>();
        public List<int> backfillRoleIds = new List<int>();
        public List<string> pathDefs = new List<string>();
        public List<int> groupRoleIds = new List<int>();
        public List<int> colorRoleIds = new List<int>();
        public List<int> holderRoleIds = new List<int>();
        public List<int> entriesRoleIds = new List<int>();
        public List<int> paletteSnapRoleIds = new List<int>();
        public bool recommendationOrder;
    }
}

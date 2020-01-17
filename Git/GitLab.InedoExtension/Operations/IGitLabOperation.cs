using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitLab.Operations
{
    internal interface IGitLabOperation
    {
        string ResourceName { get;  }
        string CredentialName { get; }
        string GroupName { get;  }
        string ProjectName { get; }
        string ApiUrl { get;  }
        SecureString Password { get; }
        string UserName { get; }
    }
}

//------------------------------------------------------------------------------
// <copyright file="XmlPatchError.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

using System;
using System.Xml;

namespace Microsoft.XmlDiffPatch
{
    internal class XmlPatchError
    {
        internal const string InvalidPathDescriptor = "Invalid XDL diffgram. '{0}' is an invalid path descriptor.";
        internal const string NoMatchingNode = "Invalid XDL diffgram. No node matches the path descriptor '{0}'.";
        internal const string ExpectingDiffgramElement = "Invalid XDL diffgram. Expecting xd:xmldiff as a root element with namespace URI '" + XmlDiff.NamespaceUri + "'.";
        internal const string MissingSrcDocAttribute = "Invalid XDL diffgram. Missing srcDocHash attribute on the xd:xmldiff element.";
        internal const string MissingOptionsAttribute = "Invalid XDL diffgram. Missing options attribute on the xd:xmldiff element.";
        internal const string InvalidSrcDocAttribute = "Invalid XDL diffgram. The srcDocHash attribute has an invalid value.";
        internal const string InvalidOptionsAttribute = "Invalid XDL diffgram. The options attribute has an invalid value.";
        internal const string SrcDocMismatch = "The XDL diffgram is not applicable to this XML document; the srcDocHash value does not match.";
        internal const string MoreThanOneNodeMatched = "Invalid XDL diffgram; more than one node matches the '{0}' path descriptor on the xd:node or xd:change element.";
        internal const string XmlDeclMismatch = "The diffgram is not applicable to this XML document; cannot add a new xml declaration.";
        internal const string ChildOperationsOnLeafNode = "The XDL diffgram is not applicable to this XML document; the path descriptor '{0}' matched a {1} node which cannot contain children, but the diffgram contains child operations for it. The document being patched does not correspond to the document the diffgram was generated for.";
        internal const string ChangeTypeMismatch = "The XDL diffgram is not applicable to this XML document; the xd:change operation with the path descriptor '{0}' does not fit the matched {1} node. The document being patched does not correspond to the document the diffgram was generated for.";
        internal const string MatchNodeTypeMismatch = "The XDL diffgram is not applicable to this XML document; the path descriptor '{0}' matched a node of type {1}, but the diffgram was generated for a node of type {2}. The document being patched does not correspond to the document the diffgram was generated for.";
        internal const string MatchNodeNameMismatch = "The XDL diffgram is not applicable to this XML document; the path descriptor '{0}' matched a node named '{1}', but the diffgram was generated for a node named '{2}'. The document being patched does not correspond to the document the diffgram was generated for.";
        internal const string MatchNodeContentMismatch = "The XDL diffgram is not applicable to this XML document; the content of the node(s) matched by the path descriptor '{0}' differs from the corresponding node(s) in the document the diffgram was generated for.";

        internal const string InternalErrorMoreThanOneNodeInList = "Internal Error. XmlDiffPathSingleNodeList can contain one node only.";
        internal const string InternalErrorMoreThanOneNodeLeft = "Internal Error. {0} nodes left after patch, expecting 1.";

        internal static void Error( string message ) {
            throw new Exception( message );
        }

        internal static void Error( string message, string arg1 ) {
            Error( String.Format( message, arg1 ) );
        }

        internal static void Error( string message, string arg1, string arg2 ) {
            Error( String.Format( message, arg1, arg2 ) );
        }

        internal static void Error( string message, string arg1, string arg2, string arg3 ) {
            Error( String.Format( message, arg1, arg2, arg3 ) );
        }
    }

}


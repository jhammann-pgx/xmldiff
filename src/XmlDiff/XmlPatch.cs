//------------------------------------------------------------------------------
// <copyright file="XmlPatch.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Diagnostics;
using Microsoft.XmlDiffPatch;

namespace Microsoft.XmlDiffPatch
{

//////////////////////////////////////////////////////////////////
// XmlPatch
//
/// <summary>
///    XML Patch modifies XML documents or nodes according to the XDL diffgram created by XML Diff.  
/// </summary>
public class XmlPatch
{
// Fields
    XmlNode _sourceRootNode;
    bool   _ignoreChildOrder;
    bool   _ignoreSrcValidation;
    bool   _enableMatchReanchoring;
    XmlDiffOptions _diffgramOptions;
    XmlHash _matchValidationHash;

    /// <summary>
    /// Construct empty XmlPatch object.
    /// </summary>
	public XmlPatch()
	{
	}

    /// <summary>
    ///    If true, the patch operation skips validation of the diffgram source hash.
    /// </summary>
    public bool IgnoreSrcValidation
    {
        get { return _ignoreSrcValidation; }
        set { _ignoreSrcValidation = value; }
    }

    /// <summary>
    ///    If true and the diffgram carries match validation data (see XmlDiff.EmitMatchValidation),
    ///    a positional match path that resolves to a different node than the one the diffgram was
    ///    generated for is re-anchored: the patcher searches the siblings for the node with the
    ///    expected content and applies the operation there. This allows a diffgram to be applied
    ///    to a document whose child order differs from the original source document (typical for
    ///    mixed content). Has no effect on diffgrams without match validation data.
    /// </summary>
    public bool EnableMatchReanchoring
    {
        get { return _enableMatchReanchoring; }
        set { _enableMatchReanchoring = value; }
    }

// Methods
    /// <summary>
    ///    Reads the XDL diffgram from the diffgramFileName and modifies the original XML document
    ///    sourceDoc according to the changes described in the diffgram. 
    /// </summary>
    /// <param name="sourceDoc">The original xml document</param>
    /// <param name="diffgram">XmlReader for the XDL diffgram.</param>
	public void Patch( XmlDocument sourceDoc, XmlReader diffgram ) 
	{
        if ( sourceDoc == null )
            throw new ArgumentNullException( "sourceDoc" );
        if ( diffgram == null )
            throw new ArgumentNullException( "diffgram" );

        XmlNode sourceNode = sourceDoc;
		Patch( ref sourceNode, diffgram );
        Debug.Assert( sourceNode == sourceDoc );
	}

    /// <summary>
    ///    Reads the XDL diffgram from the diffgramFileName and modifies the original XML document
    ///    sourceDoc according to the changes described in the diffgram. 
    /// </summary>
    /// <param name="sourceFile">The original xml document</param>
    /// <param name="outputStream">The output stream to write results to</param>
    /// <param name="diffgram">XmlReader for the XDL diffgram.</param>
    /// <param name="outputEncoding">The encoding to use in the output stream</param>
    public void Patch( string sourceFile, Stream outputStream, XmlReader diffgram, string outputEncoding = "utf-8") 
	{
        if ( sourceFile == null )
            throw new ArgumentNullException( "sourceFile" );
        if ( outputStream == null )
            throw new ArgumentNullException( "outputStream" );
        if ( diffgram == null ) 
            throw new ArgumentException( "diffgram" );

        XmlDocument diffDoc = new XmlDocument();
        diffDoc.Load( diffgram );

        var enc = Encoding.GetEncoding(outputEncoding);

        // patch fragment
        if ( diffDoc.DocumentElement.GetAttribute( "fragments" ) == "yes" ) {
            NameTable nt = new NameTable();
            using (var file = new FileStream(sourceFile, FileMode.Open, FileAccess.Read))
            {
                XmlTextReader tr = new XmlTextReader(file,
                                                    XmlNodeType.Element,
                                                    new XmlParserContext(nt, new XmlNamespaceManager(nt),
                                                                        string.Empty, XmlSpace.Default));
                Patch(tr, outputStream, diffDoc, enc);
            }
        }
        // patch document
        else {
            Patch ( new XmlTextReader( sourceFile ), outputStream, diffDoc, enc);
        }
	}

    /// <summary>
    ///    Reads the XDL diffgram from the diffgramFileName and modifies the original XML document
    ///    sourceDoc according to the changes described in the diffgram. 
    /// </summary>
    /// <param name="sourceReader">The original xml document</param>
    /// <param name="outputStream">The output stream to write results to.</param>
    /// <param name="diffgram">XmlReader for the XDL diffgram.</param>
    /// <param name="outputEncoding">The encoding to use in the output stream</param>
    public void Patch( XmlReader sourceReader, Stream outputStream, XmlReader diffgram, string outputEncoding = "utf-8") {
        if ( sourceReader == null )
            throw new ArgumentNullException( "sourceReader" );
        if ( outputStream == null )
            throw new ArgumentNullException( "outputStream" );
        if ( diffgram == null ) 
            throw new ArgumentException( "diffgram" );

        XmlDocument diffDoc = new XmlDocument();
        diffDoc.Load( diffgram );
            
        var enc = Encoding.GetEncoding(outputEncoding);
        Patch( sourceReader, outputStream, diffDoc, enc);
    }

    private void Patch( XmlReader sourceReader, Stream outputStream, XmlDocument diffDoc, Encoding enc) {
        bool bFragments = diffDoc.DocumentElement.GetAttribute( "fragments" ) == "yes"; 

        if ( bFragments ) {
            // load fragment
            XmlDocument tmpDoc = new XmlDocument();
            XmlDocumentFragment frag = tmpDoc.CreateDocumentFragment();

            XmlNode node;
            while ( ( node = tmpDoc.ReadNode( sourceReader ) ) != null ) {
                switch ( node.NodeType ) {
                    case XmlNodeType.Whitespace:
                        break;
                    case XmlNodeType.XmlDeclaration:
                        frag.InnerXml = node.OuterXml;
                        break;
                    default:
                        frag.AppendChild( node );
                        break;
                }
            }

            // patch
            XmlNode sourceNode = frag;
            Patch( ref sourceNode, diffDoc );
            Debug.Assert( sourceNode == frag );

            // save
            if ( frag.FirstChild != null && frag.FirstChild.NodeType == XmlNodeType.XmlDeclaration ) {
                enc = Encoding.GetEncoding( ((XmlDeclaration)sourceNode.FirstChild).Encoding );
            }
            XmlTextWriter tw = new XmlTextWriter(outputStream, enc);
            frag.WriteTo( tw );
            tw.Flush();
        }
        else {
            // load document
            XmlDocument sourceDoc = new XmlDocument();
            sourceDoc.XmlResolver = null;
            sourceDoc.Load( sourceReader );

            // patch
            XmlNode sourceNode = sourceDoc;
    	    Patch( ref sourceNode, diffDoc );
            Debug.Assert( sourceNode == sourceDoc );

            // save
            sourceDoc.Save( outputStream );
        }
    }


    /// <summary>
    ///    Reads the XDL diffgram from the diffgramFileName and modifies the original XML document
    ///    sourceDoc according to the changes described in the diffgram. 
    /// </summary>
    /// <param name="sourceNode">The original xml node</param>
    /// <param name="diffgram">XmlReader for the XDL diffgram.</param>
    public void Patch( ref XmlNode sourceNode, XmlReader diffgram )
    {
        if ( sourceNode == null )
            throw new ArgumentNullException( "sourceNode" );
        if ( diffgram == null )
            throw new ArgumentNullException( "diffgram" );

        XmlDocument diffDoc = new XmlDocument();
        diffDoc.Load( diffgram );

        Patch( ref sourceNode, diffDoc );
    }

    private void Patch( ref XmlNode sourceNode, XmlDocument diffDoc ) {
        XmlElement diffgramEl = diffDoc.DocumentElement;
        if ( diffgramEl.LocalName != "xmldiff" || diffgramEl.NamespaceURI != XmlDiff.NamespaceUri )
            XmlPatchError.Error( XmlPatchError.ExpectingDiffgramElement );

        XmlNamedNodeMap diffgramAttributes = diffgramEl.Attributes;
        XmlAttribute optionsAttr = (XmlAttribute) diffgramAttributes.GetNamedItem( "options" );
        if ( optionsAttr == null )
            XmlPatchError.Error( XmlPatchError.MissingOptionsAttribute );
            
        // parse options
        XmlDiffOptions xmlDiffOptions = XmlDiffOptions.None;
        try {
            xmlDiffOptions = XmlDiff.ParseOptions( optionsAttr.Value );
        }
        catch {
            XmlPatchError.Error( XmlPatchError.InvalidOptionsAttribute );
        }
        
        _ignoreChildOrder = ( (int)xmlDiffOptions & (int)XmlDiffOptions.IgnoreChildOrder ) != 0;
        _diffgramOptions = xmlDiffOptions;

        if ( !_ignoreSrcValidation )
        {
            XmlAttribute srcDocAttr = (XmlAttribute)diffgramAttributes.GetNamedItem( "srcDocHash" );
            if ( srcDocAttr == null )
                XmlPatchError.Error( XmlPatchError.MissingSrcDocAttribute );

            ulong hashValue = 0;
            try {
                hashValue = ulong.Parse( srcDocAttr.Value );
            }
            catch {
                XmlPatchError.Error( XmlPatchError.InvalidSrcDocAttribute );
            }

            // Calculate the hash value of the source document and verify that it
            // matches the srcDocHash value from the diffgram.
            if ( !XmlDiff.VerifySource( sourceNode, hashValue, xmlDiffOptions ) )
                XmlPatchError.Error( XmlPatchError.SrcDocMismatch );
        }

        // Translate diffgram & Apply patch
        if ( sourceNode.NodeType == XmlNodeType.Document ) 
        {
            Patch patch = CreatePatch( sourceNode, diffgramEl );

    		// create temporary root element and move all document children under it
            XmlDocument sourceDoc = (XmlDocument)sourceNode;
    		XmlElement tempRoot = sourceDoc.CreateElement( "tempRoot" );
	    	XmlNode child = sourceDoc.FirstChild;
    		while ( child != null )
	    	{
		    	XmlNode tmpChild = child.NextSibling;

			    if ( child.NodeType != XmlNodeType.XmlDeclaration &&
				    child.NodeType != XmlNodeType.DocumentType )
    			{
	    			sourceDoc.RemoveChild( child );
		    		tempRoot.AppendChild( child );
    			}

	    		child = tmpChild;
    		}
	    	sourceDoc.AppendChild( tempRoot );

            // Apply patch
            XmlNode temp = null;
            patch.Apply( tempRoot, ref temp );

    		// remove the temporary root element
            if ( sourceNode.NodeType == XmlNodeType.Document ) {
    		    sourceDoc.RemoveChild( tempRoot );
    	    	Debug.Assert( tempRoot.Attributes.Count == 0 );
	    	    while ( ( child = tempRoot.FirstChild ) != null )
    	    	{
	    	    	tempRoot.RemoveChild( child );
    		    	sourceDoc.AppendChild( child );
	    	    }
            }
        }
        else if ( sourceNode.NodeType == XmlNodeType.DocumentFragment ) {
            Patch patch = CreatePatch( sourceNode, diffgramEl );
            XmlNode temp = null;
            patch.Apply( sourceNode, ref temp );
        }
        else {
            // create fragment with sourceNode as its only child
            XmlDocumentFragment fragment = sourceNode.OwnerDocument.CreateDocumentFragment();
            XmlNode previousSourceParent = sourceNode.ParentNode;
            XmlNode previousSourceSibbling = sourceNode.PreviousSibling;

            if ( previousSourceParent != null ) {
                previousSourceParent.RemoveChild( sourceNode );
            }
            if ( sourceNode.NodeType != XmlNodeType.XmlDeclaration ) {
                fragment.AppendChild( sourceNode );
            }
            else {
                fragment.InnerXml = sourceNode.OuterXml;
            }

            Patch patch = CreatePatch( fragment, diffgramEl );
            XmlNode temp = null;
            patch.Apply( fragment, ref temp );

            XmlNodeList childNodes = fragment.ChildNodes;
            if ( childNodes.Count != 1 ) {
                XmlPatchError.Error( XmlPatchError.InternalErrorMoreThanOneNodeLeft, childNodes.Count.ToString() );
            }

            sourceNode = childNodes.Item(0);
            fragment.RemoveAll();
            if ( previousSourceParent != null ) {
                previousSourceParent.InsertAfter( sourceNode, previousSourceSibbling );
            }
        }
    }

    private Patch CreatePatch( XmlNode sourceNode, XmlElement diffgramElement )
	{
        Debug.Assert( sourceNode.NodeType == XmlNodeType.Document ||
                      sourceNode.NodeType == XmlNodeType.DocumentFragment );

        Patch patch = new Patch( sourceNode );

        _sourceRootNode = sourceNode;

        // create patch for <xmldiff> node children
        CreatePatchForChildren( sourceNode, 
                                diffgramElement,
                                patch );
        return patch;
    }

    private void CreatePatchForChildren( XmlNode sourceParent, 
                                         XmlElement diffgramParent, 
                                         XmlPatchParentOperation patchParent )
    {
        Debug.Assert( sourceParent != null );
        Debug.Assert( diffgramParent != null );
        Debug.Assert( patchParent != null );

        XmlPatchOperation lastPatchOp = null;

        XmlNode node = diffgramParent.FirstChild;
        while ( node != null )
        {
            if ( node.NodeType != XmlNodeType.Element ) {
                node = node.NextSibling;
                continue;
            }

            XmlElement diffOp = (XmlElement)node;
            XmlNodeList matchNodes = null;
            string matchAttr = diffOp.GetAttribute( "match" );

            if ( matchAttr != string.Empty )
            {
                matchNodes = PathDescriptorParser.SelectNodes( _sourceRootNode, sourceParent, matchAttr );

                if ( matchNodes.Count == 0 )
                    XmlPatchError.Error( XmlPatchError.NoMatchingNode, matchAttr );

                matchNodes = ResolveAndValidateMatch( matchNodes, diffOp, matchAttr );
            }

            XmlPatchOperation patchOp = null;

            switch ( diffOp.LocalName )
            {
                case "node":
                {
                    Debug.Assert( matchAttr != string.Empty );

                    if ( matchNodes.Count != 1 )
                        XmlPatchError.Error( XmlPatchError.MoreThanOneNodeMatched, matchAttr );

                    XmlNode matchNode = matchNodes.Item( 0 );

                    if ( _sourceRootNode.NodeType != XmlNodeType.Document ||
                         ( matchNode.NodeType != XmlNodeType.XmlDeclaration && matchNode.NodeType != XmlNodeType.DocumentType ) ) {
                        ValidateMatchNodeCanHostChildOperations( matchNode, diffOp, matchAttr );
                        patchOp = new PatchSetPosition( matchNode );
                        CreatePatchForChildren( matchNode, diffOp, (XmlPatchParentOperation) patchOp );
                    }
                    break;
                }
                case "add":
                {
                    // copy node/subtree
                    if ( matchAttr != string.Empty )
                    {
                        bool bSubtree = diffOp.GetAttribute( "subtree" ) != "no";
                        patchOp = new PatchCopy( matchNodes, bSubtree );
                        if ( !bSubtree )
                            CreatePatchForChildren( sourceParent, diffOp, (XmlPatchParentOperation) patchOp );
                    }
                    else
                    {
                        string type = diffOp.GetAttribute( "type" );
                        // add single node
                        if ( type != string.Empty )
                        {
                            XmlNodeType nodeType = (XmlNodeType) int.Parse( type );
                            bool bElement = (nodeType == XmlNodeType.Element);

                            if ( nodeType != XmlNodeType.DocumentType ) {
                                patchOp = new PatchAddNode( nodeType,
                                                            diffOp.GetAttribute( "name" ),
                                                            diffOp.GetAttribute( "ns" ),
                                                            diffOp.GetAttribute( "prefix" ),
                                                            bElement ? string.Empty : diffOp.InnerText,
                                                            _ignoreChildOrder );
                                if ( bElement )
                                    CreatePatchForChildren( sourceParent, diffOp, (XmlPatchParentOperation) patchOp );
                            }
                            else {
                                patchOp = new PatchAddNode( nodeType,
                                                            diffOp.GetAttribute( "name" ),
                                                            diffOp.GetAttribute( "systemId" ),
                                                            diffOp.GetAttribute( "publicId" ),
                                                            diffOp.InnerText,
                                                            _ignoreChildOrder );
                            }
                        }
                        // add blob
                        else
                        {
                            Debug.Assert( diffOp.ChildNodes.Count > 0 );
                            patchOp = new PatchAddXmlFragment( diffOp.ChildNodes );
                        }
                    }

                    break;
                }
                case "remove":
                {
                    Debug.Assert( matchAttr != string.Empty );

                    bool bSubtree = diffOp.GetAttribute( "subtree" ) != "no";
                    patchOp = new PatchRemove( matchNodes, bSubtree );
                    if ( !bSubtree )
                    {
                        Debug.Assert( matchNodes.Count == 1 );
                        ValidateMatchNodeCanHostChildOperations( matchNodes.Item(0), diffOp, matchAttr );
                        CreatePatchForChildren( matchNodes.Item(0), diffOp, (XmlPatchParentOperation) patchOp );
                    }

                    break;
                }
                case "change":
                {
                    Debug.Assert( matchAttr != string.Empty );
                    if ( matchNodes.Count != 1 )
                        XmlPatchError.Error( XmlPatchError.MoreThanOneNodeMatched, matchAttr );

                    XmlNode matchNode = matchNodes.Item( 0 );
                    ValidateChangeMatchNodeType( matchNode, diffOp, matchAttr );
                    if ( matchNode.NodeType != XmlNodeType.DocumentType ) {
                        patchOp = new PatchChange( matchNode,
                                                diffOp.HasAttribute( "name" ) ? diffOp.GetAttribute( "name" ) : null,
                                                diffOp.HasAttribute( "ns" ) ? diffOp.GetAttribute( "ns" ) : null, 
                                                diffOp.HasAttribute( "prefix" ) ? diffOp.GetAttribute( "prefix" ) : null, 
                                                (matchNode.NodeType == XmlNodeType.Element) ? null : diffOp );
                    }
                    else {
                        patchOp = new PatchChange( matchNode,
                                                   diffOp.HasAttribute( "name" ) ? diffOp.GetAttribute( "name" ) : null, 
                                                   diffOp.HasAttribute( "systemId" ) ? diffOp.GetAttribute( "systemId" ) : null,
                                                   diffOp.HasAttribute( "publicId" ) ? diffOp.GetAttribute( "publicId" ) : null,
                                                   diffOp.IsEmpty ? null : diffOp );
                    }

                    if ( matchNode.NodeType == XmlNodeType.Element )
                        CreatePatchForChildren( matchNode, diffOp, (XmlPatchParentOperation) patchOp );
                    break;
                }
                case "descriptor":
                    return;

                default:
                    Debug.Assert( false, "Invalid element in the XDL diffgram ." );
                    break;
            }

            if ( patchOp != null ) {
                patchParent.InsertChildAfter( lastPatchOp, patchOp );
                lastPatchOp = patchOp;
            }
            node = node.NextSibling;
        }
    }

    // Resolves a positional match against the matchHash metadata: when re-anchoring is enabled
    // and the node at the matched position does not have the expected content hash, the operation
    // is re-anchored to the sibling that does. When no such sibling exists, the match falls
    // through to the plain validation and fails there (or is tolerated under IgnoreSrcValidation).
    private XmlNodeList ResolveAndValidateMatch( XmlNodeList matchNodes, XmlElement diffOp, string matchAttr )
    {
        if ( _enableMatchReanchoring && matchNodes.Count == 1 )
        {
            string expectedHashStr = diffOp.GetAttribute( "matchHash" );
            ulong expectedHash;
            if ( expectedHashStr != string.Empty && ulong.TryParse( expectedHashStr, out expectedHash ) )
            {
                if ( _matchValidationHash == null )
                    _matchValidationHash = new XmlHash();

                XmlNode matchNode = matchNodes.Item( 0 );
                if ( _matchValidationHash.ComputeHash( matchNode, _diffgramOptions ) == expectedHash )
                    return matchNodes;   // hash matches -> type and name are implicitly verified

                XmlNode reanchoredNode = FindNodeByHash( matchNode, expectedHash );

                if ( reanchoredNode == null && _ignoreSrcValidation && !NodeFitsTypeAndName( matchNode, diffOp ) )
                {
                    // Under IgnoreSrcValidation the node's content may legitimately differ, so a
                    // changed node cannot be located by its hash. When the positional node does
                    // not even fit the expected type and name - typically because text or other
                    // nodes were inserted or reordered around it - fall back to the nearest
                    // sibling that does.
                    reanchoredNode = FindNodeByTypeAndName( matchNode, diffOp );
                }

                if ( reanchoredNode != null )
                {
                    XmlPatchNodeList reanchoredList = new SingleNodeList();
                    reanchoredList.AddNode( reanchoredNode );
                    return reanchoredList;
                }
            }
        }

        ValidateMatchMetadata( matchNodes, diffOp, matchAttr );
        return matchNodes;
    }

    // Scans the siblings of the mismatched node for the node whose content hash equals the
    // hash recorded in the diffgram. When several identical candidates exist, the one closest
    // to the original position is used - identical hashes mean identical content, so the
    // outcome is equivalent. Returns null when no sibling matches.
    private XmlNode FindNodeByHash( XmlNode mismatchedNode, ulong expectedHash )
    {
        XmlNode parent = mismatchedNode.ParentNode;
        if ( parent == null )
            return null;

        int originalIndex = 0;
        int index = 0;
        XmlNode child = parent.FirstChild;
        while ( child != null )
        {
            if ( child == mismatchedNode )
            {
                originalIndex = index;
                break;
            }
            index++;
            child = child.NextSibling;
        }

        XmlNode bestNode = null;
        int bestDistance = int.MaxValue;
        index = 0;
        child = parent.FirstChild;
        while ( child != null )
        {
            if ( child != mismatchedNode &&
                 _matchValidationHash.ComputeHash( child, _diffgramOptions ) == expectedHash )
            {
                int distance = ( index > originalIndex ) ? index - originalIndex : originalIndex - index;
                if ( distance < bestDistance )
                {
                    bestDistance = distance;
                    bestNode = child;
                }
            }
            index++;
            child = child.NextSibling;
        }
        return bestNode;
    }

    // True when the node fits the matchType/matchName attributes of the operation.
    // Operations without type metadata fit trivially.
    private static bool NodeFitsTypeAndName( XmlNode node, XmlElement diffOp )
    {
        string expectedTypeStr = diffOp.GetAttribute( "matchType" );
        int expectedType;
        if ( expectedTypeStr == string.Empty || !int.TryParse( expectedTypeStr, out expectedType ) )
            return true;

        if ( (int)node.NodeType != expectedType )
            return false;

        string expectedName = diffOp.GetAttribute( "matchName" );
        if ( expectedName == string.Empty )
            return true;

        string actualName = ( node.NodeType == XmlNodeType.Element ) ? node.LocalName : node.Name;
        return actualName == expectedName;
    }

    // Weaker re-anchoring used with IgnoreSrcValidation, where content differences are permitted
    // and a changed node cannot be located by its hash: finds the sibling closest to the original
    // position that fits the expected node type and name. Returns null when no sibling fits.
    private XmlNode FindNodeByTypeAndName( XmlNode mismatchedNode, XmlElement diffOp )
    {
        XmlNode parent = mismatchedNode.ParentNode;
        if ( parent == null )
            return null;

        int originalIndex = 0;
        int index = 0;
        XmlNode child = parent.FirstChild;
        while ( child != null )
        {
            if ( child == mismatchedNode )
            {
                originalIndex = index;
                break;
            }
            index++;
            child = child.NextSibling;
        }

        XmlNode bestNode = null;
        int bestDistance = int.MaxValue;
        index = 0;
        child = parent.FirstChild;
        while ( child != null )
        {
            if ( child != mismatchedNode && NodeFitsTypeAndName( child, diffOp ) )
            {
                int distance = ( index > originalIndex ) ? index - originalIndex : originalIndex - index;
                if ( distance < bestDistance )
                {
                    bestDistance = distance;
                    bestNode = child;
                }
            }
            index++;
            child = child.NextSibling;
        }
        return bestNode;
    }

    // Verifies the matched node(s) against the matchType/matchName/matchHash attributes that
    // XmlDiff emits when EmitMatchValidation is enabled. The match path descriptors are positional,
    // so this is what detects a diffgram being applied to a document whose child order differs
    // from the document the diffgram was generated for. The content hash is not checked when
    // IgnoreSrcValidation is set - the caller has declared that the documents may differ in
    // content - but node type and name are still verified.
    private void ValidateMatchMetadata( XmlNodeList matchNodes, XmlElement diffOp, string matchAttr )
    {
        string expectedTypeStr = diffOp.GetAttribute( "matchType" );
        if ( expectedTypeStr != string.Empty && matchNodes.Count == 1 )
        {
            XmlNode matchNode = matchNodes.Item( 0 );

            int expectedType;
            if ( int.TryParse( expectedTypeStr, out expectedType ) && (int)matchNode.NodeType != expectedType )
                XmlPatchError.Error( XmlPatchError.MatchNodeTypeMismatch, matchAttr,
                                     matchNode.NodeType.ToString(), ((XmlNodeType)expectedType).ToString() );

            string expectedName = diffOp.GetAttribute( "matchName" );
            if ( expectedName != string.Empty )
            {
                string actualName = ( matchNode.NodeType == XmlNodeType.Element ) ? matchNode.LocalName : matchNode.Name;
                if ( actualName != expectedName )
                    XmlPatchError.Error( XmlPatchError.MatchNodeNameMismatch, matchAttr, actualName, expectedName );
            }
        }

        if ( !_ignoreSrcValidation )
        {
            string expectedHashStr = diffOp.GetAttribute( "matchHash" );
            ulong expectedHash;
            if ( expectedHashStr != string.Empty && ulong.TryParse( expectedHashStr, out expectedHash ) )
            {
                if ( _matchValidationHash == null )
                    _matchValidationHash = new XmlHash();

                ulong actualHash = 0;
                for ( int i = 0; i < matchNodes.Count; i++ )
                    actualHash += _matchValidationHash.ComputeHash( matchNodes.Item( i ), _diffgramOptions );

                if ( actualHash != expectedHash )
                    XmlPatchError.Error( XmlPatchError.MatchNodeContentMismatch, matchAttr );
            }
        }
    }

    private static bool HasChildOperations( XmlElement diffOp )
    {
        XmlNode child = diffOp.FirstChild;
        while ( child != null ) {
            if ( child.NodeType == XmlNodeType.Element )
                return true;
            child = child.NextSibling;
        }
        return false;
    }

    // The match paths in a diffgram are positional; when the document being patched does not have
    // the same child order as the document the diffgram was generated for (typical for mixed content),
    // a path can silently resolve to a node of a different type. Descending into a node that cannot
    // have children would otherwise surface as a misleading NoMatchingNode error deeper in the tree.
    private static void ValidateMatchNodeCanHostChildOperations( XmlNode matchNode, XmlElement diffOp, string matchAttr )
    {
        if ( !HasChildOperations( diffOp ) )
            return;

        switch ( matchNode.NodeType ) {
            case XmlNodeType.Text:
            case XmlNodeType.CDATA:
            case XmlNodeType.Comment:
            case XmlNodeType.Whitespace:
            case XmlNodeType.SignificantWhitespace:
            case XmlNodeType.ProcessingInstruction:
            case XmlNodeType.XmlDeclaration:
            case XmlNodeType.DocumentType:
                XmlPatchError.Error( XmlPatchError.ChildOperationsOnLeafNode, matchAttr, matchNode.NodeType.ToString() );
                break;
        }
    }

    // Verifies that the payload of an xd:change operation fits the type of the matched node.
    // A mismatch (e.g. a text-value change resolving to an element) would otherwise be applied
    // to the wrong node or silently do nothing.
    private static void ValidateChangeMatchNodeType( XmlNode matchNode, XmlElement diffOp, string matchAttr )
    {
        bool hasNameAttributes = diffOp.HasAttribute( "name" ) || diffOp.HasAttribute( "ns" ) || diffOp.HasAttribute( "prefix" );
        XmlNode payload = diffOp.FirstChild;

        switch ( matchNode.NodeType )
        {
            case XmlNodeType.Element:
                // element changes are renames (name/ns/prefix) with nested child operations;
                // a character data payload without rename attributes was generated for a non-element node
                if ( !hasNameAttributes && payload != null &&
                     ( payload.NodeType == XmlNodeType.Text ||
                       payload.NodeType == XmlNodeType.CDATA ||
                       payload.NodeType == XmlNodeType.Comment ||
                       payload.NodeType == XmlNodeType.ProcessingInstruction ) )
                    XmlPatchError.Error( XmlPatchError.ChangeTypeMismatch, matchAttr, matchNode.NodeType.ToString() );
                break;

            case XmlNodeType.Text:
            case XmlNodeType.CDATA:
            case XmlNodeType.Whitespace:
            case XmlNodeType.SignificantWhitespace:
                // value changes carry the new text as plain content and no rename attributes
                if ( hasNameAttributes ||
                     ( payload != null &&
                       ( payload.NodeType == XmlNodeType.Comment ||
                         payload.NodeType == XmlNodeType.ProcessingInstruction ||
                         payload.NodeType == XmlNodeType.Element ) ) )
                    XmlPatchError.Error( XmlPatchError.ChangeTypeMismatch, matchAttr, matchNode.NodeType.ToString() );
                break;

            case XmlNodeType.Comment:
                // comment changes carry the new value as a comment node
                if ( hasNameAttributes || payload == null || payload.NodeType != XmlNodeType.Comment )
                    XmlPatchError.Error( XmlPatchError.ChangeTypeMismatch, matchAttr, matchNode.NodeType.ToString() );
                break;

            case XmlNodeType.ProcessingInstruction:
                // PI changes carry either a name attribute (rename) or the new PI as payload
                if ( !diffOp.HasAttribute( "name" ) &&
                     ( payload == null || payload.NodeType != XmlNodeType.ProcessingInstruction ) )
                    XmlPatchError.Error( XmlPatchError.ChangeTypeMismatch, matchAttr, matchNode.NodeType.ToString() );
                break;
        }
    }
}

}

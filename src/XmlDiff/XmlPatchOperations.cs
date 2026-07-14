//------------------------------------------------------------------------------
// <copyright file="XmlPatchOperations.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
//------------------------------------------------------------------------------

namespace Microsoft.XmlDiffPatch
{

using System;
using System.Diagnostics;
using System.Xml;
using System.IO;
using System.Text;
using System.Collections;

//////////////////////////////////////////////////////////////////
// XmlPatchOperation	
//
internal abstract class XmlPatchOperation
{
// Fields
    internal XmlPatchOperation _nextOp;

// Methods
    internal abstract void Apply( XmlNode parent, ref XmlNode currentPosition );

    // Called by XmlPatch when a leading add operation should take the place of a following
    // removed node instead of being inserted at the beginning of the parent (replacement
    // semantics under match re-anchoring). No-op for operations that do not insert content.
    internal virtual void SetLeadingInsertAnchor( XmlNode insertBeforeNode ) {}
}

//////////////////////////////////////////////////////////////////
// XmlPatchParentOperation 
//
internal abstract class XmlPatchParentOperation : XmlPatchOperation
{
// Fields
    internal XmlPatchOperation _firstChild;

// Methods
    internal void InsertChildAfter( XmlPatchOperation child, XmlPatchOperation newChild )
    {
        Debug.Assert( newChild != null );

        if ( child == null )
        {
            newChild._nextOp = _firstChild;
            _firstChild = newChild;
        }
        else
        {
            newChild._nextOp = child._nextOp;
            child._nextOp = newChild;
        }
    }

    protected void ApplyChildren( XmlNode parent )
    {
        XmlNode curPos = null;
        XmlPatchOperation curOp = _firstChild;

        while ( curOp != null )
        {
            curOp.Apply( parent, ref curPos );
            curOp = curOp._nextOp;
        }
    }
}

//////////////////////////////////////////////////////////////////
// PatchSetPosition
//
internal class PatchSetPosition : XmlPatchParentOperation
{
// Fields
    XmlNode _matchNode;

// Constructor
    internal PatchSetPosition( XmlNode matchNode ) 
    {
        Debug.Assert( matchNode != null );
        _matchNode = matchNode;
    }
// Methods
    internal override void Apply( XmlNode parent, ref XmlNode currentPosition )
    {
        Debug.Assert( _matchNode.NodeType != XmlNodeType.DocumentType );
        Debug.Assert( _matchNode.ParentNode == parent );

        if ( _matchNode.NodeType == XmlNodeType.Element )
            ApplyChildren( (XmlElement)_matchNode );

        currentPosition = _matchNode;
    }
}

//////////////////////////////////////////////////////////////////
// PatchCopy
//
internal class PatchCopy : XmlPatchParentOperation
{
// Fields
    XmlNodeList _matchNodes;
    bool _bSubtree;
    bool _bAnchorLast;
    XmlNode _insertBeforeNode;

// Constructor
    internal PatchCopy( XmlNodeList matchNodes, bool bSubtree, bool bAnchorLast )
    {
        Debug.Assert( matchNodes != null );
        Debug.Assert( matchNodes.Count != 0 );

        _matchNodes = matchNodes;
        _bSubtree = bSubtree;
        _bAnchorLast = bAnchorLast;
    }

    internal override void SetLeadingInsertAnchor( XmlNode insertBeforeNode )
    {
        _insertBeforeNode = insertBeforeNode;
    }

// Methods
    internal override void Apply( XmlNode parent, ref XmlNode currentPosition )
    {
        IEnumerator e = _matchNodes.GetEnumerator();
        e.Reset();
        while ( e.MoveNext() )
        {
            XmlNode srcNode = (XmlNode)e.Current;
            Debug.Assert( srcNode.NodeType != XmlNodeType.Attribute );

            XmlNode newNode;
            if ( _bSubtree )
                newNode = srcNode.Clone();
            else
            {
                newNode = srcNode.CloneNode( false );
                ApplyChildren( newNode );
            }

            if ( _bAnchorLast )
                parent.AppendChild( newNode );
            else if ( currentPosition == null && _insertBeforeNode != null )
                parent.InsertBefore( newNode, _insertBeforeNode );
            else
                parent.InsertAfter( newNode, currentPosition );
            currentPosition = newNode;
        }
    }
}

//////////////////////////////////////////////////////////////////
// PatchAddNode
//
internal class PatchAddNode : XmlPatchParentOperation
{
// Fields
    XmlNodeType _nodeType;
    string _name;
    string _ns;         // == systemId if DocumentType node
    string _prefix;     // == publicId if DocumentType node
    string _value;      // == internal subset if DocumentType node

    bool   _ignoreChildOrder;
    bool   _bAnchorLast;
    XmlNode _insertBeforeNode;

// Constructor
    internal PatchAddNode( XmlNodeType nodeType, string name, string ns, string prefix, string value, bool ignoreChildOrder, bool bAnchorLast )
    {
        Debug.Assert( (int)nodeType > 0 && (int)nodeType <= (int)XmlNodeType.XmlDeclaration );

        _nodeType = nodeType;
        _name = name;
        _ns = ns;
        _prefix = prefix;
        _value = value;

        _ignoreChildOrder = ignoreChildOrder;
        _bAnchorLast = bAnchorLast;
    }

    internal override void SetLeadingInsertAnchor( XmlNode insertBeforeNode )
    {
        _insertBeforeNode = insertBeforeNode;
    }

// Methods
    internal override void Apply( XmlNode parent, ref XmlNode currentPosition )
    {
        XmlNode newNode = null;

		if ( _nodeType == XmlNodeType.Attribute )
		{
			Debug.Assert( _name != string.Empty );
			if ( _prefix == "xmlns" )
				newNode = parent.OwnerDocument.CreateAttribute( _prefix + ":" + _name );
			else if (_prefix == "" && _name == "xmlns" ) 
				newNode = parent.OwnerDocument.CreateAttribute( _name );
			else
				newNode = parent.OwnerDocument.CreateAttribute( _prefix, _name, _ns );
			((XmlAttribute)newNode).Value = _value;

            Debug.Assert( currentPosition == null );
            parent.Attributes.Append( (XmlAttribute) newNode );
		}
		else
		{
			switch ( _nodeType )
			{
				case XmlNodeType.Element:
					Debug.Assert( _name != string.Empty );
					Debug.Assert( _value == string.Empty );
					newNode = parent.OwnerDocument.CreateElement( _prefix, _name, _ns );
					ApplyChildren( newNode );
					break;
				case XmlNodeType.Text:
					Debug.Assert( _value != string.Empty );
					newNode = parent.OwnerDocument.CreateTextNode( _value );
					break;
				case XmlNodeType.CDATA:
					Debug.Assert( _value != string.Empty );
					newNode = parent.OwnerDocument.CreateCDataSection( _value );
					break;
				case XmlNodeType.Comment:
					Debug.Assert( _value != string.Empty );
					newNode = parent.OwnerDocument.CreateComment( _value );
					break;
				case XmlNodeType.ProcessingInstruction:
					Debug.Assert( _value != string.Empty );
					Debug.Assert( _name != string.Empty );
					newNode = parent.OwnerDocument.CreateProcessingInstruction( _name, _value );
					break;
				case XmlNodeType.EntityReference:
					Debug.Assert( _name != string.Empty );
					newNode = parent.OwnerDocument.CreateEntityReference( _name );
					break;
                case XmlNodeType.XmlDeclaration:
                {
                    Debug.Assert( _value != string.Empty );
                    XmlDocument doc = parent.OwnerDocument;
                    XmlDeclaration decl = doc.CreateXmlDeclaration( "1.0", string.Empty, string.Empty );
                    decl.Value = _value;
                    doc.InsertBefore( decl, doc.FirstChild );
                    return;
                }
                case XmlNodeType.DocumentType:
                {
                    XmlDocument doc = parent.OwnerDocument;
                    if ( _prefix == string.Empty )
                        _prefix = null;
                    if ( _ns == string.Empty ) 
                        _ns = null;
                    XmlDocumentType docType = doc.CreateDocumentType( _name, _prefix, _ns, _value );
                    if ( doc.FirstChild.NodeType == XmlNodeType.XmlDeclaration )
                        doc.InsertAfter( docType, doc.FirstChild );
                    else
                        doc.InsertBefore( docType, doc.FirstChild );
                    return;
                }
				default:
					Debug.Assert( false ); 
					break;
			}

			Debug.Assert( currentPosition == null || currentPosition.NodeType != XmlNodeType.Attribute );

            if ( _ignoreChildOrder || _bAnchorLast ) {
                parent.AppendChild( newNode );
            }
            else if ( currentPosition == null && _insertBeforeNode != null ) {
                parent.InsertBefore( newNode, _insertBeforeNode );
            }
            else {
                parent.InsertAfter( newNode, currentPosition );
            }
			currentPosition = newNode;
		}
    }
}

//////////////////////////////////////////////////////////////////
// PatchAddXmlFragment
//
internal class PatchAddXmlFragment : XmlPatchOperation
{
// Fields
    XmlNodeList _nodes;
    bool _bAnchorLast;
    XmlNode _insertBeforeNode;

// Constructor
    internal PatchAddXmlFragment( XmlNodeList nodes, bool bAnchorLast )
    {
        Debug.Assert( nodes != null );
        _nodes = nodes;
        _bAnchorLast = bAnchorLast;
    }

    internal override void SetLeadingInsertAnchor( XmlNode insertBeforeNode )
    {
        _insertBeforeNode = insertBeforeNode;
    }

// Methods
    internal override void Apply( XmlNode parent, ref XmlNode currentPosition )
    {
        XmlDocument doc = parent.OwnerDocument;

        IEnumerator enumerator = _nodes.GetEnumerator();
        while ( enumerator.MoveNext() )
        {
            XmlNode newNode = doc.ImportNode( (XmlNode)enumerator.Current, true );
            if ( _bAnchorLast )
                parent.AppendChild( newNode );
            else if ( currentPosition == null && _insertBeforeNode != null )
                parent.InsertBefore( newNode, _insertBeforeNode );
            else
                parent.InsertAfter( newNode, currentPosition );
            currentPosition = newNode;
        }
    }
}

//////////////////////////////////////////////////////////////////
// PatchRemove
//
internal class PatchRemove : XmlPatchParentOperation
{
// Fields
    XmlNodeList _sourceNodes;
    bool _bSubtree;

// Constructor
    internal PatchRemove( XmlNodeList sourceNodes, bool bSubtree ) 
    {
        Debug.Assert( sourceNodes != null );
        Debug.Assert( sourceNodes.Count > 0 );
        _sourceNodes = sourceNodes;
        _bSubtree = bSubtree;
    }

// Methods
    internal override void Apply( XmlNode parent, ref XmlNode currentPosition )
    {
        if ( !_bSubtree )
        {
            Debug.Assert( _sourceNodes.Count == 1 );

			XmlNode remNode = _sourceNodes.Item(0);
            ApplyChildren( remNode );

            currentPosition = remNode.PreviousSibling;
        }

        IEnumerator e = _sourceNodes.GetEnumerator();
        e.Reset();
        while ( e.MoveNext() )
        {
            XmlNode node = (XmlNode)e.Current;

            Debug.Assert( node.ParentNode == parent ||
                        ( node.ParentNode == null && node.NodeType == XmlNodeType.Attribute ) ||
                        ( node.NodeType == XmlNodeType.XmlDeclaration && node.ParentNode == node.OwnerDocument ) ||
                        ( node.NodeType == XmlNodeType.DocumentType && node.ParentNode == node.OwnerDocument) );
            
            if ( node.NodeType == XmlNodeType.Attribute )
            {
                Debug.Assert( parent.NodeType == XmlNodeType.Element );
                ((XmlElement)parent).RemoveAttributeNode( (XmlAttribute) node );
            }
            else
            {
                if ( !_bSubtree )
                {
                    // move all children to grandparent
                    while ( node.FirstChild != null )
                    {
                        XmlNode child = node.FirstChild;
                        node.RemoveChild( child );
                        parent.InsertAfter( child, currentPosition );
                        currentPosition = child;
                    }
                }

                // remove node
                node.ParentNode.RemoveChild( node );  // this is node.ParentNode instead of node.parent because of the xml declaration
            }
        }
    }
}

//////////////////////////////////////////////////////////////////
// PatchChange
//
internal class PatchChange : XmlPatchParentOperation
{
// Fields
    XmlNode _matchNode;
    string _name;
    string _ns;      // = systemId if DocumentType node
    string _prefix;  // = publicId if DocumentType node
    string _value;   // = internal subset if DocumentType node

// Constructor
    internal PatchChange( XmlNode matchNode, string name, string ns, string prefix, XmlNode diffChangeNode ) 
    {
        Debug.Assert( matchNode != null );

        _matchNode = matchNode;
        _name = name;
        _ns = ns;
        _prefix = prefix;

        if ( diffChangeNode == null ) {
            _value = null;
        }
        else {
            switch ( matchNode.NodeType ) {
                case XmlNodeType.Comment:
                    Debug.Assert( diffChangeNode.FirstChild != null && diffChangeNode.FirstChild.NodeType == XmlNodeType.Comment );
                    _value = diffChangeNode.FirstChild.Value;
                    break;
                case XmlNodeType.ProcessingInstruction:
                    if ( name == null ) {
                        Debug.Assert( diffChangeNode.FirstChild != null && diffChangeNode.FirstChild.NodeType == XmlNodeType.ProcessingInstruction );
                        _name = diffChangeNode.FirstChild.Name;
                        _value = diffChangeNode.FirstChild.Value;
                    }
                    break;
                default:
                    _value = diffChangeNode.InnerText;
                    break;
            }
        }
    }

// Methods
    internal override void Apply( XmlNode parent, ref XmlNode currentPosition )
    {
        Debug.Assert( _matchNode.ParentNode == parent ||
                     ( _matchNode.ParentNode == null && _matchNode.NodeType == XmlNodeType.Attribute ) || 
                       _matchNode.NodeType == XmlNodeType.XmlDeclaration ||
                       _matchNode.NodeType == XmlNodeType.DocumentType );

        switch ( _matchNode.NodeType )
        {
            case XmlNodeType.Element:
            {
                Debug.Assert( _value == null );

                if ( _name == null )   _name = ((XmlElement)_matchNode).LocalName;
                if ( _ns == null )     _ns = ((XmlElement)_matchNode).NamespaceURI;
                if ( _prefix == null ) _prefix = ((XmlElement)_matchNode).Prefix;

                XmlElement newEl = parent.OwnerDocument.CreateElement( _prefix, _name, _ns );

				// move attributes
				XmlAttributeCollection attrs = _matchNode.Attributes;
				while ( attrs.Count > 0 )
				{
					XmlAttribute attr = (XmlAttribute)attrs.Item( 0 );
					attrs.RemoveAt( 0 );
					newEl.Attributes.Append( attr );
				}

				// move children
                XmlNode curChild = _matchNode.FirstChild;
                while ( curChild != null )
                {
                    XmlNode nextSibling = curChild.NextSibling;
                    _matchNode.RemoveChild( curChild );
                    newEl.AppendChild( curChild );
                    curChild = nextSibling;
                }

                parent.ReplaceChild( newEl, _matchNode );
                currentPosition = newEl;

                ApplyChildren( newEl );

                break;
            }
            case XmlNodeType.Attribute:
            {
                if ( _name == null )   _name = ((XmlAttribute)_matchNode).LocalName;
                if ( _ns == null )     _ns = ((XmlAttribute)_matchNode).NamespaceURI;
                if ( _prefix == null ) _prefix = ((XmlAttribute)_matchNode).Prefix;
                if ( _value == null )  _value = ((XmlAttribute)_matchNode).Value;

                XmlAttribute newAttr = parent.OwnerDocument.CreateAttribute( _prefix, _name, _ns );
                newAttr.Value = _value;

                parent.Attributes.Remove( (XmlAttribute)_matchNode );
                parent.Attributes.Append( newAttr );
                break;
            }
            case XmlNodeType.Text:
            case XmlNodeType.CDATA:
            case XmlNodeType.Comment:
                Debug.Assert( _value != null );
                ((XmlCharacterData)_matchNode).Data = _value;
                currentPosition = _matchNode;
                break;
            case XmlNodeType.ProcessingInstruction:
            {
                if ( _name != null ) 
                {
                    if ( _value == null )  
                        _value = ((XmlProcessingInstruction)_matchNode).Data;
                    XmlProcessingInstruction newPi = parent.OwnerDocument.CreateProcessingInstruction( _name, _value );

                    parent.ReplaceChild( newPi, _matchNode );
                    currentPosition = newPi;
                }
                else
                {
                    ((XmlProcessingInstruction)_matchNode).Data = _value;
                    currentPosition = _matchNode;
                }
                break;
            }
            case XmlNodeType.EntityReference:
            {
                Debug.Assert( _name != null );
                
                XmlEntityReference newEr = parent.OwnerDocument.CreateEntityReference( _name );
                
                parent.ReplaceChild( newEr, _matchNode );
                currentPosition = newEr;
                break;
            }
            case XmlNodeType.XmlDeclaration:
            {
                Debug.Assert( _value != null && _value != string.Empty );
                XmlDeclaration xmlDecl = (XmlDeclaration)_matchNode;
                xmlDecl.Encoding = null;
                xmlDecl.Standalone = null;
                xmlDecl.InnerText = _value;
                break;
            }
            case XmlNodeType.DocumentType:
            {
                if ( _name == null )
                    _name = ((XmlDocumentType)_matchNode).LocalName;

                if ( _ns == null )
                    _ns = ((XmlDocumentType)_matchNode).SystemId;
                else if ( _ns == string.Empty )
                    _ns = null;

                if ( _prefix == null ) 
                    _prefix = ((XmlDocumentType)_matchNode).PublicId;
                else if ( _prefix == string.Empty ) 
                    _prefix = null;

                if ( _value == null )  
                    _value = ((XmlDocumentType)_matchNode).InternalSubset;

                XmlDocumentType docType = _matchNode.OwnerDocument.CreateDocumentType( _name, _prefix, _ns, _value );
                _matchNode.ParentNode.ReplaceChild( docType, _matchNode );
                break;
            }
            default:
                Debug.Assert( false ); 
                break;
        }
    }
}

//////////////////////////////////////////////////////////////////
// Patch
//
internal class Patch : XmlPatchParentOperation
{
// Fields
    internal XmlNode _sourceRootNode;

// Constructor
    internal Patch( XmlNode sourceRootNode )
    {
        Debug.Assert( sourceRootNode != null );
        _sourceRootNode = sourceRootNode;
    }

// Methods
    internal override void Apply( XmlNode parent, ref XmlNode currentPosition )
    {
        XmlDocument doc = parent.OwnerDocument;

        ApplyChildren( parent );
    }
}

} 
<?xml version="1.0" encoding="iso-8859-1"?>
<xsl:stylesheet version="1.1" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:content="http://purl.org/rss/1.0/modules/content/"
	xmlns:wfw="http://wellformedweb.org/CommentAPI/"
	xmlns:dc="http://purl.org/dc/elements/1.1/"
	xmlns:atom="http://www.w3.org/2005/Atom"
	xmlns:sy="http://purl.org/rss/1.0/modules/syndication/"
	xmlns:slash="http://purl.org/rss/1.0/modules/slash/"
	xmlns:itunes="http://www.itunes.com/dtds/podcast-1.0.dtd"
	xmlns:feedburner="http://rssnamespace.org/feedburner/ext/1.0">
  <xsl:output method="html" />
  <xsl:variable name="title" select="/rss/channel/title"/>

  <xsl:template match="/">
    <html>
      <head>
        <title>
          <xsl:value-of select="$title"/> XML Feed
        </title>        
        <link rel="stylesheet" href="//cdnjs.cloudflare.com/ajax/libs/foundation/5.3.0/css/foundation.min.css" type="text/css"/>
      </head>
      <xsl:apply-templates select="rss/channel"/>
    </html>
  </xsl:template>

  <xsl:template match="channel">
    <body>
      <h1>
        <a href="{link}">
          <xsl:value-of select="$title"/>
        </a>
      </h1>
      <h3>
        <xsl:value-of select="/rss/channel/description"/>
      </h3>
      <table>
        <xsl:apply-templates select="item"/>
      </table>
    </body>
  </xsl:template>

  <xsl:template match="item">
    <tr>
      <td>
        <h2>
          <a href="{link}">
            <xsl:value-of disable-output-escaping="yes" select="title"/>
          </a>
        </h2>
        <div>
          <xsl:choose>
            <xsl:when test="content:encoded!=''">
              <xsl:value-of disable-output-escaping="yes" select="content:encoded" />
            </xsl:when>
            <xsl:otherwise>
              <xsl:value-of disable-output-escaping="yes" select="description" />
            </xsl:otherwise>
          </xsl:choose>
        </div>
      </td>
    </tr>
  </xsl:template>
</xsl:stylesheet>

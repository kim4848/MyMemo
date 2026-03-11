function inlineMarkdown(text: string): string {
  return text
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\*(.+?)\*/g, '<em>$1</em>')
    .replace(/`(.+?)`/g, '<code>$1</code>');
}

export function memoToHtml(content: string): string {
  const lines = content.split('\n');
  const htmlLines: string[] = [];
  let inList = false;

  for (const line of lines) {
    const headingMatch = line.match(/^(#{1,6})\s+(.*)/);
    if (headingMatch) {
      if (inList) { htmlLines.push('</ul>'); inList = false; }
      const level = headingMatch[1].length;
      htmlLines.push(`<h${level}>${inlineMarkdown(headingMatch[2])}</h${level}>`);
      continue;
    }

    const listMatch = line.match(/^[-*]\s+(.*)/);
    if (listMatch) {
      if (!inList) { htmlLines.push('<ul>'); inList = true; }
      htmlLines.push(`<li>${inlineMarkdown(listMatch[1])}</li>`);
      continue;
    }

    if (inList) { htmlLines.push('</ul>'); inList = false; }
    htmlLines.push(line.trim() ? `<p>${inlineMarkdown(line)}</p>` : '<p>&nbsp;</p>');
  }

  if (inList) htmlLines.push('</ul>');
  return htmlLines.join('\n');
}

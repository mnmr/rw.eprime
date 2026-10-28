"""Stable Markdown translation units. Parsing/patching runs only in offline tools.

IDs belong to content, not positions or hashes. The reserved title ID addresses
frontmatter; body IDs travel with their standalone <!-- l10n:ID --> comments.
Offsets refer to the original text, preserving BOMs, line endings and spacing.
"""
from dataclasses import dataclass
import hashlib
import json
import re
import uuid

MARKER = re.compile(r'^<!-- l10n:([a-zA-Z0-9][a-zA-Z0-9_-]*) -->[ \t]*(?:\r?\n|$)', re.MULTILINE)
HEADER = re.compile(r'\A\ufeff?---\r?\n(.*?)\r?\n---(?:\r?\n|$)', re.DOTALL)
TITLE = re.compile(r'^title:[ \t]*(\S[^\r\n]*)', re.MULTILINE)
ITEM = re.compile(r'^(?:- |[1-9][0-9]*\. )')
HEADING = re.compile(r'^(#{1,6}) ')


@dataclass(frozen=True)
class Block:
    id: str
    kind: str
    text: str
    start: int
    end: int
    marker_start: int = 0


def kind(text):
    first = text.strip().splitlines()[0]
    heading = HEADING.match(first)
    if heading:
        return f'heading{len(heading[1])}'
    if re.fullmatch(r'!\[[^\]]*\]\([^)\n]+\)', first):
        return 'image'
    if first.startswith('@'):
        return 'directive'
    if ITEM.match(first):
        return 'list'
    return 'paragraph'


def spans(text, offset=0):
    """Paragraphs, headings, captions, directives and contiguous related lists."""
    lines = list(re.finditer(r'[^\r\n]+(?:\r?\n|$)|\r?\n', text))
    result, i = [], 0
    while i < len(lines):
        if not lines[i][0].strip():
            i += 1
            continue
        first = i
        category = kind(lines[i][0])
        i += 1
        if category in ('paragraph', 'list'):
            while i < len(lines):
                line = lines[i][0].strip()
                if not line:
                    # Blank lines between list items don't split the related list.
                    if category == 'list' and i + 1 < len(lines) and ITEM.match(lines[i + 1][0].strip()):
                        i += 1
                        continue
                    break
                following = kind(line)
                if following != 'paragraph' and not (category == 'list' and following == 'list'):
                    break
                i += 1
        start, end = lines[first].start(), lines[i - 1].end()
        raw = text[start:end]
        start += len(raw) - len(raw.lstrip())
        end -= len(raw) - len(raw.rstrip())
        result.append(Block('', category, text[start:end], offset + start, offset + end))
    return result


def title(text):
    header = HEADER.match(text)
    match = TITLE.search(header[1]) if header else None
    if not match:
        raise ValueError('Missing help title/frontmatter')
    if len(re.findall(r'^\s*title:', header[1], re.MULTILINE)) != 1:
        raise ValueError('Help frontmatter must contain exactly one title')
    start = header.start(1) + match.start(1)
    return Block('title', 'title', match[1].rstrip(), start, start + len(match[1].rstrip()))


def parse(text):
    """None denotes legacy whole-page data; malformed tracked pages fail closed."""
    if '<!-- l10n:' not in text:
        return None
    heading = title(text)
    header_end = HEADER.match(text).end()
    markers = list(MARKER.finditer(text))
    if not markers or text[header_end:markers[0].start()].strip():
        raise ValueError('Unmarked help content before the first block')
    blocks, seen = [heading], {'title'}
    for i, marker in enumerate(markers):
        identifier = marker[1]
        if identifier in seen:
            raise ValueError(f'Duplicate/reserved help block ID: {identifier}')
        seen.add(identifier)
        end = markers[i + 1].start() if i + 1 < len(markers) else len(text)
        content = text[marker.end():end]
        if '<!-- l10n:' in content:
            raise ValueError(f'Malformed help block marker after {identifier}')
        found = spans(content, marker.end())
        if len(found) != 1:
            raise ValueError(f'Block {identifier} must contain exactly one paragraph, heading, image, directive or related list')
        block = found[0]
        blocks.append(Block(identifier, block.kind, block.text, block.start, block.end, marker.start()))
    return blocks


def canonical(text):
    # Preserve block/list boundaries but ignore soft wrapping within paragraphs
    # and list items. Hard Markdown breaks are not part of the game's dialect.
    lines, pending = [], ''
    for line in text.replace('\r\n', '\n').strip().split('\n'):
        line = line.strip()
        if not line or kind(line) != 'paragraph':
            if pending:
                lines.append(pending)
            pending = line
        else:
            pending = pending + ' ' + line if pending else line
    if pending:
        lines.append(pending)
    return '\n'.join(lines)


def dependencies(blocks):
    headings, intro, result = [], None, {}
    for block in blocks:
        if block.kind.startswith('heading'):
            level = int(block.kind[-1])
            headings = [(depth, key) for depth, key in headings if depth < level]
            intro = None
        deps = [key for _, key in headings]
        if block.kind == 'list' and intro:
            deps.append(intro)
        result[block.id] = deps
        if block.kind.startswith('heading'):
            headings.append((int(block.kind[-1]), block.id))
        if block.kind == 'paragraph':
            intro = block.id
        elif block.kind == 'list':
            intro = None
    return result


def digests(blocks):
    values = {b.id: canonical(b.text) for b in blocks}
    deps = dependencies(blocks)
    return {b.id: hashlib.sha256(json.dumps([b.kind, values[b.id],
            [(key, values[key]) for key in deps[b.id]]], ensure_ascii=False).encode('utf-8')).hexdigest()
            for b in blocks}


def signature(block):
    """Language-independent alignment evidence; ambiguity is never guessed."""
    return (block.kind, re.findall(r'^(?:- |[1-9][0-9]*\. )', block.text, re.MULTILINE),
            re.findall(r'\]\(([^)\n]+)\)', block.text), re.findall(r'`([^`]+)`', block.text),
            sorted(re.findall(r'\{[^{}]+\}', block.text)),
            block.text.strip() if block.kind == 'directive' else '')


def annotate(text, identifiers=None):
    """Insert IDs without changing any existing character."""
    if parse(text) is not None:
        raise ValueError('Help already has block IDs')
    title(text)
    body = spans(text[HEADER.match(text).end():], HEADER.match(text).end())
    ids = identifiers if identifiers is not None else ['b-' + uuid.uuid4().hex[:12] for _ in body]
    if len(ids) != len(body):
        raise ValueError('Help block counts differ; manual alignment required')
    newline = '\r\n' if '\r\n' in text else '\n'
    for block, identifier in reversed(list(zip(body, ids))):
        text = text[:block.start] + f'<!-- l10n:{identifier} -->' + newline + text[block.start:]
    parse(text)
    return text


def patch(source, current, updated):
    """Assemble source order from exact existing target chunks; edit only requests."""
    source_blocks = parse(source)
    if source_blocks is None:
        raise ValueError('Expected tracked English help')
    if 'title' in updated and (not updated['title'].strip() or '\n' in updated['title'] or '\r' in updated['title']):
        raise ValueError('Translated title must be one nonempty line')
    old = parse(current) if current else []
    if old is None:
        raise ValueError('Legacy target needs help migration before block updates')
    old_by_id = {b.id: b for b in old}
    newline = '\r\n' if '\r\n' in current else '\n'
    prefix = current[:old[1].marker_start] if len(old) > 1 else (current or '---' + newline + 'title: ' + updated['title'] + newline + '---' + newline)
    if old and 'title' in updated:
        b = old[0]
        prefix = prefix[:b.start] + updated['title'].strip() + prefix[b.end:]
    chunks, previous = [], None
    old_next = {b.id: old[i + 1].id if i + 1 < len(old) else None for i, b in enumerate(old)}
    source_kinds = {b.id: b.kind for b in source_blocks}
    for block in source_blocks[1:]:
        if block.id in old_by_id:
            b = old_by_id[block.id]
            index = old.index(b)
            end = old[index + 1].marker_start if index + 1 < len(old) else len(current)
            chunk = current[b.marker_start:end]
            if block.id in updated and updated[block.id] != b.text:
                value = updated[block.id].strip().replace('\r\n', '\n').replace('\n', newline)
                chunk = chunk[:b.start - b.marker_start] + value + chunk[b.end - b.marker_start:]
        else:
            if block.id not in updated:
                raise ValueError(f'Missing translated block: {block.id}')
            value = updated[block.id].strip().replace('\r\n', '\n').replace('\n', newline)
            chunk = f'<!-- l10n:{block.id} -->{newline}{value}{newline}{newline}'
        if chunks and (old_next.get(previous) != block.id or
                       (previous in old_by_id and old_by_id[previous].kind != source_kinds[previous])):
            # A formerly final block may have only one trailing newline. When
            # it gains a neighbour, keep the two rendered paragraphs separate.
            if not chunks[-1].endswith(newline):
                chunks[-1] += newline
            if not chunks[-1].endswith(newline + newline):
                chunks[-1] += newline
        chunks.append(chunk)
        previous = block.id
    result = prefix + ''.join(chunks)
    parsed = parse(result)
    if parsed is None or [b.id for b in parsed] != [b.id for b in source_blocks]:
        raise ValueError('Translated help changed block structure')
    for original, translated in zip(source_blocks, parsed):
        if original.kind != translated.kind:
            raise ValueError(f'Translated block {original.id} changed kind')
    return result

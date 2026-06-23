import re, sys

with open(sys.argv[1], 'r', encoding='utf-8') as f:
    content = f.read()

# Replace complete blocks from "container.Register(" to the matching ");"
# that contain SvcDescriptor.Create and SvcLifetime.Singleton

def replace_register_blocks(text):
    result = []
    i = 0
    replacements = 0
    lines = text.split('\n')
    
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()
        
        if stripped.startswith('container.Register('):
            # Collect the block until matching ;)
            block_lines = [line]
            depth = stripped.count('(') - stripped.count(')')
            j = i + 1
            while j < len(lines) and depth > 0:
                l = lines[j]
                block_lines.append(l)
                depth += l.count('(') - l.count(')')
                j += 1
            
            block = '\n'.join(block_lines)
            
            # Check if this is a SvcDescriptor.Create pattern with SvcLifetime.Singleton
            if 'SvcDescriptor.Create(' in block and 'SvcLifetime.Singleton' in block:
                # Extract the type and factory
                type_match = re.search(r'typeof\((\w+)\)', block)
                if type_match:
                    type_name = type_match.group(1)
                    
                    # Extract factory - everything between the type and SvcLifetime.Singleton
                    factory_match = re.search(
                        r'typeof\(\w+\)\s*,\s*([\s\S]+?)\s*,\s*SvcLifetime\.Singleton',
                        block
                    )
                    if factory_match:
                        factory_raw = factory_match.group(1).strip()
                        
                        # Handle Func wrapper
                        m1 = re.match(r'\(Func<ISvcScope,\s*object>\)\s*\((.+)\)\s*$', factory_raw)
                        if m1:
                            factory_raw = m1.group(1).strip()
                        elif '_ => (object)new ' in factory_raw:
                            factory_raw = factory_raw.replace('_ => (object)new ', '_ => new ')
                        elif '_ => (object)' in factory_raw:
                            parts = factory_raw.split('_ => (object)')
                            if len(parts) == 2:
                                factory_raw = f'_ => {parts[1]}'
                        
                        replacement = f'        container.RegisterHostedSvc<{type_name}>({factory_raw});'
                        result.append(replacement)
                        replacements += 1
                        i = j  # Skip the processed lines
                        continue
            
            # If not matched, keep original lines
            result.extend(block_lines)
            i = j
        else:
            result.append(line)
            i += 1
    
    return '\n'.join(result), replacements

result, count = replace_register_blocks(content)
with open(sys.argv[1], 'w', encoding='utf-8') as f:
    f.write(result)
print(f'Replaced {count} blocks')

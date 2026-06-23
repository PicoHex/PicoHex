import os, sys

# Files to process and line numbers to SKIP (keep annotation)
# Format: {filename: {set_of_skip_line_numbers}}
skip_lines = {
    # SvcDescriptor.cs: keep FromInstance line
    'SvcDescriptor.cs': {89},
    # SvcContainerHostingExtensions.cs: keep the 2 RegisterHostedSvc(Type) overloads
    'SvcContainerHostingExtensions.cs': {54, 77},
}

for fname in [
    'PicoDI/src/PicoDI/SvcRuntimeRegistration.cs',
    'PicoDI/src/PicoDI.Abs/SvcDescriptor.cs',
    'PicoDI/src/PicoDI.Abs/SvcContainerGeneralRegistrationExtensions.cs',
    'PicoDI/src/PicoDI.Abs/SvcContainerLifetimeRegistrationExtensions.cs',
    'PicoDI/src/PicoDI.Abs/SvcContainerHostingExtensions.cs',
]:
    basename = os.path.basename(fname)
    with open(fname, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    skip = skip_lines.get(basename, set())
    new_lines = []
    removed = 0
    
    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()
        line_num = i + 1  # 1-indexed
        
        # Check if this line has the annotation
        has_dam = 'DynamicallyAccessedMembers' in stripped
        
        if has_dam and line_num in skip:
            new_lines.append(line)
            i += 1
            continue
        
        if has_dam:
            # Remove this annotation line
            removed += 1
            # Check if annotation is on its own line (ends with ] after closing paren)
            if stripped.startswith('[') and stripped.endswith(')]'):
                # Annotation on its own line - skip it
                i += 1
                continue
            elif 'Type ' in stripped or 'Type?' in stripped:
                # Remove just the [DAM] prefix, keeping the rest of the line
                # pattern: [...Annotation...] Type serviceType,
                idx = stripped.find(']')
                if idx != -1:
                    rest = stripped[idx+1:].strip()
                    # Keep the original indentation
                    indent = line[:len(line) - len(line.lstrip())]
                    new_lines.append(f'{indent}{rest}\n')
                    i += 1
                    continue
            elif stripped.startswith('[') and ('TService' in stripped or 'T,' in stripped or 'T>' in stripped or 'THostedSvc' in stripped or stripped.strip().endswith('T')):
                # Remove just the [DAM] prefix from generic parameter
                idx = stripped.find(']')
                if idx != -1:
                    rest = stripped[idx+1:].strip()
                    indent = line[:len(line) - len(line.lstrip())]
                    new_lines.append(f'{indent}{rest}\n')
                    i += 1
                    continue
        
        new_lines.append(line)
        i += 1
    
    with open(fname, 'w', encoding='utf-8') as f:
        f.writelines(new_lines)
    
    print(f'{basename}: removed {removed}, kept {len([l for l in lines if "DynamicallyAccessedMembers" in l]) - removed}')

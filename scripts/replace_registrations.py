import sys

with open(sys.argv[1], 'r', encoding='utf-8') as f:
    content = f.read()

replacements = {
    # Pattern 1: ISvcA/ISvcB/ISvcC with OrderedSvc(log, name)
    'container.Register(\n            SvcDescriptor.Create(\n                typeof(ISvcA),\n                _ => (object)new OrderedSvc(log, "A"),\n                SvcLifetime.Singleton\n            )\n        );': 'container.RegisterHostedSvc<ISvcA>(_ => new OrderedSvc(log, "A"));',
    'container.Register(\n            SvcDescriptor.Create(\n                typeof(ISvcB),\n                _ => (object)new OrderedSvc(log, "B"),\n                SvcLifetime.Singleton\n            )\n        );': 'container.RegisterHostedSvc<ISvcB>(_ => new OrderedSvc(log, "B"));',  
    'container.Register(\n            SvcDescriptor.Create(\n                typeof(ISvcC),\n                _ => (object)new OrderedSvc(log, "C"),\n                SvcLifetime.Singleton\n            )\n        );': 'container.RegisterHostedSvc<ISvcC>(_ => new OrderedSvc(log, "C"));',

    # Pattern 2: ISvcA with different values
    'container.Register(\n            SvcDescriptor.Create(\n                typeof(ISvcA),\n                _ => (object)new OrderedSvc(log, "A"),\n                SvcLifetime.Singleton\n            )\n        )': 'container.RegisterHostedSvc<ISvcA>(_ => new OrderedSvc(log, "A"))',
    'container.Register(\n            SvcDescriptor.Create(\n                typeof(ISvcB),\n                _ => (object)new OrderedSvc(log, "B"),\n                SvcLifetime.Singleton\n            )\n        )': 'container.RegisterHostedSvc<ISvcB>(_ => new OrderedSvc(log, "B"))',
    'container.Register(\n            SvcDescriptor.Create(\n                typeof(ISvcC),\n                _ => (object)new OrderedSvc(log, "C"),\n                SvcLifetime.Singleton\n            )\n        )': 'container.RegisterHostedSvc<ISvcC>(_ => new OrderedSvc(log, "C"))',
}

for old, new in sorted(replacements.items(), key=lambda x: -len(x[0])):
    if old in content:
        content = content.replace(old, new)
        print(f"Replaced: {old[:40]}...")

with open(sys.argv[1], 'w', encoding='utf-8') as f:
    f.write(content)

function Test-Command {
    param([string]$Name)
    return [bool](Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

function Resolve-Node {
    if ($env:NODE_BIN) {
        return $env:NODE_BIN
    }

    if (Test-Command 'node') {
        return 'node'
    }

    if (Test-Command 'mise') {
        $miseNode = & mise which node 2>$null
        if ($miseNode -and (Test-Path $miseNode)) {
            return $miseNode
        }
    }

    throw "Required command not found on PATH: node. Set NODE_BIN to a Node.js executable if your shell cannot find node."
}

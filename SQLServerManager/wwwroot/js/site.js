window.initializeMermaid = function () {
    mermaid.initialize({
        startOnLoad: true,
        theme: 'default',
        securityLevel: 'loose',
        er: {
            useMaxWidth: false
        }
    });
};

window.renderMermaidDiagram = function (definition) {
    console.log("Rendering diagram with definition:", definition);
    try {
        // Clear and recreate the mermaid container
        const container = document.getElementById('mermaidDiagram');
        container.innerHTML = '';

        // Create a new div with unique ID
        const diagramDiv = document.createElement('div');
        diagramDiv.className = 'mermaid';
        diagramDiv.textContent = definition;
        container.appendChild(diagramDiv);

        // Re-initialize mermaid
        mermaid.init();
    } catch (error) {
        console.error('Error rendering diagram:', error);
    }
};


window.renderMermaidDiagram = function (definition) {
    var element = document.getElementById("mermaidDiagram");
    if (!element) return;

    element.innerHTML = '';

    mermaid.initialize({
        startOnLoad: true,
        theme: 'default',
        securityLevel: 'loose',
        er: {
            useMaxWidth: true,
            fontSize: 14,
            labelPosition: 'center',
            layoutDirection: 'TB'
        }
    });

    try {
        mermaid.render('mermaid-diagram', definition, function (svgCode) {
            element.innerHTML = svgCode;
        });
    } catch (error) {
        console.error('Error rendering diagram:', error);
        element.innerHTML = '<div class="alert alert-danger">Error rendering diagram</div>';
    }
};

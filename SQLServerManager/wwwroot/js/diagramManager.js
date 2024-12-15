

window.diagramManager = {
    // Biến để theo dõi vị trí của bảng
    currentTop: 50,
    currentLeft: 50,
    spacing: 20, // Khoảng cách giữa các bảng

    init: function () {
        if (typeof jsPlumb === "undefined") {
            console.error("jsPlumb is not defined. Ensure the jsPlumb library is loaded before this script.");
            return;
        }

        jsPlumb.ready(function () {
            jsPlumb.setContainer("diagramCanvas");

            // Kích hoạt draggable cho các phần tử có class "table-item"
            jsPlumb.draggable(".table-item", {
                containment: "parent"
            });

            // Lắng nghe sự kiện kết nối
            jsPlumb.bind("connection", function (info) {
                console.log("Connection created between: ", info.sourceId, "and", info.targetId);
            });
        });
    },

    addTable: function (tableName) {
        if (typeof jsPlumb === "undefined") {
            console.error("jsPlumb is not defined. Cannot add table.");
            return;
        }

        const canvas = document.getElementById("diagramCanvas");
        if (!canvas) {
            console.error("Diagram canvas not found.");
            return;
        }

        // Tạo bảng mới
        const tableDiv = document.createElement("div");
        tableDiv.className = "table-item";
        tableDiv.id = tableName;

        // Đặt vị trí động cho bảng mới
        tableDiv.style.position = "absolute";
        tableDiv.style.top = this.currentTop + "px";
        tableDiv.style.left = this.currentLeft + "px";
        tableDiv.innerHTML = `<strong>${tableName}</strong>`;

        // Cập nhật vị trí cho bảng tiếp theo
        this.currentTop += 100; // Di chuyển xuống 100px
        if (this.currentTop > canvas.offsetHeight - 100) {
            // Nếu vượt quá chiều cao canvas, quay lại đầu và di chuyển sang phải
            this.currentTop = 50;
            this.currentLeft += 200; // Di chuyển sang phải 200px
        }

        // Thêm bảng vào khu vực sơ đồ
        canvas.appendChild(tableDiv);

        // Kích hoạt draggable cho bảng mới
        jsPlumb.draggable(tableDiv, { containment: "parent" });
    },

    drawConnections: function (connections) {
        if (typeof jsPlumb === "undefined") {
            console.error("jsPlumb is not defined. Cannot draw connections.");
            return;
        }

        if (!connections || connections.length === 0) {
            console.warn("No connections provided to draw.");
            return;
        }

        // Xóa tất cả các kết nối hiện tại
        jsPlumb.deleteEveryConnection();

        // Vẽ các kết nối mới
        connections.forEach(connection => {
            jsPlumb.connect({
                source: connection.sourceTable, // ID của bảng nguồn
                target: connection.targetTable, // ID của bảng đích
                anchors: ["Bottom", "Top"], // Neo đường nối (có thể tùy chỉnh)
                overlays: [
                    ["Arrow", { width: 10, length: 10, location: 1 }], // Mũi tên
                    ["Label", { label: connection.foreignKeyColumn, location: 0.5 }] // Nhãn hiển thị tên khóa ngoại
                ],
                connector: ["Bezier", { curviness: 50 }], // Đường nối cong
                paintStyle: { stroke: "#007bff", strokeWidth: 5 }, // Màu sắc và độ dày đường nối
                endpointStyle: { fill: "#007bff", radius: 5 } // Endpoint là hình tròn
            });
        });
    },

    updateDiagram: function (tables, connections) {
        const canvas = document.getElementById("diagramCanvas");

        if (!canvas) {
            console.error("Diagram canvas not found.");
            return;
        }

        // Xóa tất cả nội dung cũ trên canvas
        canvas.innerHTML = "";
        jsPlumb.deleteEveryConnection(); // Xóa tất cả các kết nối hiện tại

        // Reset vị trí
        this.currentTop = 50;
        this.currentLeft = 50;

        // Thêm các bảng vào sơ đồ
        tables.forEach(table => {
            this.addTable(table.name); // Giả sử mỗi bảng có thuộc tính "name"
        });

        // Vẽ các kết nối giữa các bảng
        this.drawConnections(connections);
    },
    exportAsImage: function () {
        const svgElement = document.querySelector("#diagramCanvas svg");

        if (!svgElement) {
            console.error("SVG element not found.");
            return;
        }

        // Chuyển SVG thành Canvas
        const svgData = new XMLSerializer().serializeToString(svgElement);
        const svgBlob = new Blob([svgData], { type: "image/svg+xml;charset=utf-8" });
        const url = URL.createObjectURL(svgBlob);

        const img = new Image();
        img.onload = function () {
            const canvas = document.createElement("canvas");
            canvas.width = img.width;
            canvas.height = img.height;

            const ctx = canvas.getContext("2d");
            ctx.drawImage(img, 0, 0);

            // Sau khi chuyển SVG thành Canvas, sử dụng html2canvas để chụp
            html2canvas(document.getElementById("diagramCanvas"), { scale: 2 })
                .then(canvas => {
                    const imageURL = canvas.toDataURL("image/png");
                    const downloadLink = document.createElement("a");
                    downloadLink.href = imageURL;
                    downloadLink.download = "diagram.png";
                    downloadLink.click();
                })
                .catch(error => {
                    console.error("Failed to export diagram as image:", error);
                });

            URL.revokeObjectURL(url); // Giải phóng bộ nhớ
        };
        img.src = url;
    }
}
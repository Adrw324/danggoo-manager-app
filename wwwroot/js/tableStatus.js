"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/tablehub", {
    skipNegotiation: true,
    transport: signalR.HttpTransportType.WebSockets
}).build();

connection.on("ReceiveWebSocketMessage", function (tableId, message) {
    console.log(`Received message for table ${tableId}:`, message);
    updateAllTables(); // 모든 테이블 업데이트
});

connection.on("UpdateConnectionStatus", function (data) {
    console.log("Received connection status update:", data);
    if (data && data.connectedTables) {
        data.connectedTables.forEach(tableId => updateTableUI(tableId, true));
    }
});


connection.on("UpdateTableStatus", function (tableId, isActive) {
    console.log(`Received table status update: Table ${tableId} is ${isActive ? 'active' : 'inactive'}`);
    updateTableUI(tableId, isActive);
});

connection.on("GameStarted", function (tableId) {
    console.log(`Game started on table ${tableId}`);
    updateTableUI(tableId, true);
    getGameList(tableId);
});

connection.on("GameEnded", function (tableId) {
    console.log(`Game ended on table ${tableId}`);
    updateTableUI(tableId, true);
    getGameList(tableId);
});

connection.on("ForceStartGame", function (tableId) {
    console.log(`Force start game signal received for table ${tableId}`);
    // 여기서 태블릿에 강제 시작 신호를 보내는 로직을 구현할 수 있습니다.
});

connection.on("ForceEndGame", function (tableId) {
    console.log(`Force end game signal received for table ${tableId}`);
    // 여기서 태블릿에 강제 종료 신호를 보내는 로직을 구현할 수 있습니다.
});

connection.start().then(function () {
    console.log("SignalR Connected");
    return connection.invoke("GetAllTableStatus");
}).then(function() {
    console.log("Initial table status retrieved");
    updateAllTables();
}).catch(function (err) {
    return console.error(err.toString());
});

document.addEventListener('DOMContentLoaded', updateAllTables);

function updateGameList(tableId, games) {
    var gameList = document.getElementById(`game-list-${tableId}`);
    if (gameList) {
        gameList.innerHTML = '';
        games.forEach((game, index) => {
            var listItem = document.createElement('li');
            listItem.className = 'list-group-item';
            listItem.innerHTML = `
                <strong>Game ${index + 1}</strong><br>
                시작: ${formatDate(game.start)}<br>
                종료: ${game.finished ? formatDate(game.end) : '진행 중'}<br>
                요금: $${game.fee.toFixed(2)}<br>
                <button class="btn btn-sm btn-primary mt-2" onclick="editGame(${game.id})">Edit</button>
                <button class="btn btn-sm btn-success mt-2 ml-2" onclick="saveGame(${game.id}, ${tableId})">Save</button>
                <button class="btn btn-sm btn-danger mt-2 ml-2" onclick="deleteGame(${game.id}, ${tableId})">Delete</button>
            `;
            gameList.appendChild(listItem);
        });
        
        var saveAllButton = document.createElement('button');
        saveAllButton.className = 'btn btn-info mt-3';
        saveAllButton.textContent = 'Save All';
        saveAllButton.onclick = () => saveAllGames(tableId);
        gameList.appendChild(saveAllButton);
    }
}

function getGameList(tableId) {
    fetch(`/Games/GetGameList/${tableId}`)
    .then(response => response.json())
    .then(games => {
        updateGameList(tableId, games);
    })
    .catch(error => console.error('Error:', error));
}

function formatDate(dateString) {
    return new Date(dateString).toLocaleString();
}

function updateTableUI(tableId, isActive) {
    var tableCard = document.getElementById("table-" + tableId);
    var statusText = document.getElementById("status-" + tableId);

    if (tableCard && statusText) {
        if (isActive) {
            tableCard.classList.remove("bg-light");
            tableCard.classList.add("bg-success");
            statusText.textContent = "상태: 사용 중";
        } else {
            tableCard.classList.remove("bg-success");
            tableCard.classList.add("bg-light");
            statusText.textContent = "상태: 비활성";
        }
    } else {
        console.error(`Elements for table ${tableId} not found`);
    }
}

function saveGame(gameId, tableId) {
    fetch(`/Games/SaveGame/${gameId}`, {
        method: 'POST',
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            alert('Game saved successfully.');
            getGameList(tableId);
        } else {
            alert('Failed to save game: ' + data.message);
        }
    })
    .catch(error => console.error('Error:', error));
}


function editGame(gameId) {
    window.open(`/Games/Edit/${gameId}`, 'EditGame', 'width=600,height=400');
    // 팝업 창이 닫힐 때 이벤트를 감지합니다.
}

function deleteGame(gameId, tableId) {
    if (confirm('Are you sure you want to delete this game?')) {
        fetch(`/Games/DeleteGame/${gameId}`, {
            method: 'POST',
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                alert('Game deleted successfully.');
                getGameList(tableId);
            } else {
                alert('Failed to delete game: ' + data.message);
            }
        })
        .catch(error => console.error('Error:', error));
    }
}

function saveAllGames(tableId) {
    if (confirm('Are you sure you want to move all games to records?')) {

        console.log("save all games in table " + tableId);
        fetch(`/Games/MoveAllGamesToRecords/${tableId}`, {
            method: 'POST',
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                alert('All games moved to records successfully.');
                getGameList(tableId);
            } else {
                alert('Failed to move games to records: ' + data.message);
            }
        })
        .catch(error => console.error('Error:', error));
    }
}


function startGame(tableId) {
    fetch('/Games/StartGame', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ tableNum: tableId })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            console.log('Game started:', data.game);
            updateTableUI(tableId, true);
            getGameList(tableId);
        } else {
            console.error('Failed to start game:', data.message);
        }
    })
    .catch(error => console.error('Error:', error));
}

function endGame(tableId) {
    fetch('/Games/EndGame', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ tableNum: tableId })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            console.log('Game ended:', data.game);
            updateTableUI(tableId, false);
            getGameList(tableId);
        } else {
            console.error('Failed to end game:', data.message);
        }
    })
    .catch(error => console.error('Error:', error));
}

function forceStartGame(tableId) {
    fetch(`/Games/ForceStartGame/${tableId}`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ tableNum: tableId })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            console.log('Force start game signal sent for table:', tableId);
        } else {
            console.error('Failed to send force start game signal:', data.message);
        }
    })
    .catch(error => console.error('Error:', error));
}

function forceEndGame(tableId) {
    fetch(`/Games/ForceEndGame/${tableId}`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ tableNum: tableId })
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            console.log('Force end game signal sent for table:', tableId);
        } else {
            console.error('Failed to send force end game signal:', data.message);
        }
    })
    .catch(error => console.error('Error:', error));
}
function updateAllTables() {
    for (let i = 1; i <= 12; i++) {
        getGameList(i);
    }
}


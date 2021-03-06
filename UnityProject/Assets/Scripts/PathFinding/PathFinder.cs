﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PathFinding
{
	public class PathFinder : MonoBehaviour
	{
		private RegisterTile registerTile;
		private Matrix matrix => registerTile.Matrix;
		private CustomNetTransform customNetTransform;
		public enum Status { idle, searching }
		public Status status;
		private Node startNode;
		private Node goalNode;
		private bool pathFound = false;
		PriorityQueue<Node> frontierNodes;
		private List<Node> exploredNodes = new List<Node>();
		private Dictionary<Vector2Int, Node> allNodes = new Dictionary<Vector2Int, Node>();

		private Action<List<Node>> pathFoundCallBack;
		private Action failedCallBack;
		private bool isComplete = false;


		private void Awake()
		{
			registerTile = GetComponent<RegisterTile>();
			customNetTransform = GetComponent<CustomNetTransform>();
		}

		///<summary>
		/// Use local positions related to the matrix the object is on
		///</summary>
		public void FindNewPath(Vector2Int startPos, Vector2Int targetPos,
			Action<List<Node>> pathCallBack, Action failedPathCallBack)
		{
			pathFoundCallBack = pathCallBack;
			failedCallBack = failedPathCallBack;

			startNode = new Node
			{
				position = startPos
			};

			goalNode = new Node
			{
				position = targetPos
			};

			isComplete = false;

			frontierNodes = new PriorityQueue<Node>();
			frontierNodes.Enqueue(startNode);
			exploredNodes.Clear();
			allNodes.Clear();
			allNodes.Add(startNode.position, startNode);
			allNodes.Add(goalNode.position, goalNode);
			StartCoroutine(SearchForRoute());

		}

		IEnumerator SearchForRoute()
		{
			status = Status.searching;
			yield return YieldHelper.EndOfFrame;

			while (!isComplete)
			{
				if (frontierNodes.Count > 0)
				{
					Node currentNode = frontierNodes.Dequeue();

					if (currentNode.neighbors.Count == 0)
					{
						yield return FindNeighbours(currentNode);
					}

					if (!exploredNodes.Contains(currentNode))
					{
						exploredNodes.Add(currentNode);
					}

					ExpandFrontierAStar(currentNode);

					if (frontierNodes.Contains(goalNode))
					{
						isComplete = true;
						List<Node> path = new List<Node>();
						path.Add(goalNode);

						Node nextNode = goalNode.previous;

						while (nextNode != null)
						{
							path.Insert(0, nextNode);
							nextNode = nextNode.previous;
							yield return YieldHelper.EndOfFrame;
						}

						pathFoundCallBack.Invoke(path);
					}
				}
				else
				{
					isComplete = true;
					failedCallBack.Invoke();
				}
			}
			
			status = Status.idle;
			Debug.Log("Search complete");
		}

		IEnumerator FindNeighbours(Node currentNode)
		{
			Vector2Int startPos = currentNode.position + new Vector2Int(-1, 1);
			List<Node> newNeighbours = new List<Node>();

			for (int i = 0; i < 3; i++)
			{
				for (int y = 0; y < 3; y++)
				{
					Vector2Int searchPos = new Vector2Int(startPos.x + i, startPos.y - y);
					if (searchPos == currentNode.position)
					{
						continue;
					}
					if (!allNodes.ContainsKey(searchPos))
					{
						Node newNode = new Node
						{
							position = searchPos
						};
						allNodes.Add(newNode.position, newNode);
						newNeighbours.Add(newNode);
					}
					else
					{
						newNeighbours.Add(allNodes[searchPos]);
					}
				}
			}

			yield return YieldHelper.EndOfFrame;
			currentNode.neighbors = newNeighbours;
		}

		private void ExpandFrontierAStar(Node node)
		{
			if (node != null)
			{
				for (int i = 0; i < node.neighbors.Count; i++)
				{
					if (!exploredNodes.Contains(node.neighbors[i]))
					{
						RefreshNodeType(node.neighbors[i]);

						if(node.neighbors[i].nodeType == NodeType.Blocked)
						{
							continue;
						}

						float distanceToNeighbor = GetNodeDistance(node, node.neighbors[i]);
						float newDistanceTraveled = distanceToNeighbor + node.distanceTraveled +
							(int)node.nodeType;

						if (float.IsPositiveInfinity(node.neighbors[i].distanceTraveled) ||
							newDistanceTraveled < node.neighbors[i].distanceTraveled)
						{
							node.neighbors[i].previous = node;
							node.neighbors[i].distanceTraveled = newDistanceTraveled;
						}

						if (!frontierNodes.Contains(node.neighbors[i]))
						{
							int distanceToGoal = (int)GetNodeDistance(node.neighbors[i], goalNode);
							node.neighbors[i].priority = (int)node.neighbors[i].distanceTraveled +
								distanceToGoal;
							frontierNodes.Enqueue(node.neighbors[i]);
						}
					}
				}
			}
		}

		private void RefreshNodeType(Node node)
		{
			Vector3Int checkPos = new Vector3Int(node.position.x,
				node.position.y, 0);

			if (matrix.IsPassableAt(checkPos))
			{
				node.nodeType = NodeType.Open;
				return;
			}
			else
			{
				var getDoor = matrix.GetFirst<DoorController>(checkPos);
				if (!getDoor)
				{
					//TODO: Door consideration will work if you change this condition back to doors
					//It is off for the time being as npcs that can't use doors keeps trying to make a path through them

					//node.nodeType = NodeType.Door; 

					//So block doors for the meantime:
					node.nodeType = NodeType.Blocked;
				}
				else
				{
					node.nodeType = NodeType.Blocked;
				}
			}
		}

		private float GetNodeDistance(Node source, Node target)
		{
			int dx = Mathf.Abs(source.position.x - target.position.x);
			int dy = Mathf.Abs(source.position.y - target.position.y);

			int min = Mathf.Min(dx, dy);
			int max = Mathf.Max(dx, dy);

			int diagonalSteps = min;
			int straightSteps = max - min;

			return (1.4f * diagonalSteps + straightSteps);
		}
	}
}
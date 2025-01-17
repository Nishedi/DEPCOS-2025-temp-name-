﻿using Gurobi;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class GurobiVRP


{
    public List<List<int>> subSets = new List<List<int>>();
    private static GRBEnv env = new GRBEnv();

    // Function that generates all possible combinations of customers
    public void GetCombination(List<Customer> customers)
    {
        List<int> list = new List<int>();
        foreach (var loc in customers)
        {
            if (loc.Id != 0)
                list.Add(loc.Id);
        }
        double count = Math.Pow(2, list.Count);
        for (int i = 1; i <= count - 1; i++)
        {
            string str = Convert.ToString(i, 2).PadLeft(list.Count, '0');
            List<int> subSet = new List<int>();
            for (int j = 0; j < str.Length; j++)
            {
                if (str[j] == '1')
                {
                    subSet.Add(list[j]);
                }
            }
            if (subSet.Count > 1)
                subSets.Add(subSet);
        }
    }

    public void printSubSets()
    {
        foreach (var subSet in subSets)
        {
            foreach (var item in subSet)
            {
                Console.Write(item + " ");
            }
            Console.WriteLine();
        }
    }
    public double gurobi_test(VRPTW problem)
    {
        // Generate all possible combinations
        GetCombination(problem.Customers);
        // Create a new model
        GRBModel model = new GRBModel(env);
        int locationsNumber = problem.Customers.Count;
        GRBVar[, ,] x = new GRBVar[problem.NumberOfVehicles, locationsNumber, locationsNumber];
        GRBVar[,] y = new GRBVar[problem.NumberOfVehicles, locationsNumber];
        GRBVar[,] t = new GRBVar[problem.NumberOfVehicles, locationsNumber];

        // Create variable x[v, i, j]
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for(int i = 0; i < locationsNumber; i++)
            {
                for (int j = 0; j < locationsNumber; j++)
                {
                    x[v, i, j] = model.AddVar(0.0, 1.0, 0.0, GRB.BINARY, "x_" + v + "_" + i + "_" + j);
                }
            }
        }

        // Create variable y[v, i]
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                y[v, i] = model.AddVar(0.0, 1.0, 0.0, GRB.BINARY, "y_" + v + "_" + i);

            }
        }

        // Create variable t[v, i]
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                t[v, i] = model.AddVar(0.0, 1000.0, 0.0, GRB.INTEGER, "t_" + v + "_" + i);

            }
        }

        model.Update();

        // Set objective
        GRBLinExpr expr = 0.0;
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                for (int j = 0; j < locationsNumber; j++)
                {
                    expr += x[v, i, j] * problem.distanceMatrix[i,j];
                }
            }
            //Add service time
            for (int i = 0; i < locationsNumber; i++)
            {
                expr += y[v, i] * problem.Customers[i].ServiceTime;
            }
        }
        model.SetObjective(expr, GRB.MINIMIZE);

        // Add constraint 2
        // Time constraint
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            GRBLinExpr sum = 0.0;
            // Add travelling time
            for (int i = 0; i < locationsNumber; i++)
            {
                for (int j = 0; j < locationsNumber; j++)
                {
                    sum += x[v, i, j] * problem.distanceMatrix[i, j];
                }
            }
            //Add service time
            for (int i = 0; i < locationsNumber; i++)
            {
                sum += y[v, i] * problem.Customers[i].ServiceTime;
            }  
            // Checking if the time is less than the vehicle working time. Vehicle 0 is take, because fleet is homogeneous
            model.AddConstr(sum, GRB.LESS_EQUAL, problem.Vehicles[0].wv, "c2");
        }

        // Add constraint 3 
        // A location must be assigned to exactly one vehicle
        for (int i = 1; i < locationsNumber; i++)
        {
            GRBLinExpr sum = 0.0;
            for (int v = 0; v < problem.NumberOfVehicles; v++)
                {
                sum += y[v, i];
            }
            model.AddConstr(sum, GRB.EQUAL, 1.0, "c3");
        }

        // Add constraint 4
        // Setting x variables depending on y variables
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                GRBLinExpr sum = 0.0;
                for (int j = 0; j < locationsNumber; j++)
                {
                    sum += x[v, j, i];
                }
                model.AddConstr(y[v,i], GRB.LESS_EQUAL, sum, "c4");
            }
        }

        // Add constraint 5
        // Vehicle must start from depot
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                GRBLinExpr sum = 0.0;
                for (int j = 0; j < locationsNumber; j++)
                {
                    sum += x[v, 0, j];
                }
                model.AddConstr(y[v, i], GRB.LESS_EQUAL, sum, "c5");
            }

        }


        // Add constraint 6
        // A vehicle must not go to the same location
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            GRBLinExpr sum = 0.0;
            for (int i = 0; i < locationsNumber; i++)
            {
                sum += x[v, i, i];
            }
                model.AddConstr(sum, GRB.EQUAL, 0.0, "c6");
        }

        // Add constraint 7
        // A vehicle must enter and leave a location
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                GRBLinExpr sumEnter = 0.0;
                GRBLinExpr sumLeave = 0.0;
                for (int j = 0; j < locationsNumber; j++)
                {
                    sumEnter += x[v, i, j];
                    sumLeave += x[v, j, i];
                }
                model.AddConstr(sumEnter, GRB.EQUAL, sumLeave, "c7");
            }
        }

        // Add constraint 8
        // No subtour constraint
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            foreach (var subSet in subSets)
            {
                GRBLinExpr sum = 0.0;
                foreach (var i in subSet)
                {
                    foreach (var j in subSet)
                    {
                        if (i != j)
                            sum += x[v, i, j];
                    }
                }
                model.AddConstr(sum, GRB.LESS_EQUAL, subSet.Count - 1, "c8");
            }
        }

        // Add constraint 9
        // Set time variables
        /*
        for (int v = 0; v < problem.NumberOfVehicles; v++)
        {
            for (int i = 0; i < locationsNumber; i++)
            {
                for(int j = 0; j < locationsNumber; j++)
                {
                    GRBLinExpr sum = (t[v, i] + problem.distanceMatrix[i, j] + problem.Customers[j].ServiceTime);
                    model.AddConstr(t[v, j], GRB.EQUAL, sum, "c9");
                }
            }
        }
        */

        // Optimize model
        model.Optimize();
        // Print model variables
        GRBVar[] vars = model.GetVars();
        foreach (var v in vars)
            Console.WriteLine(v.VarName + " = " + v.X);
        double fCelu = model.ObjVal;
        //model.Write("model.lp");

        // Dispose (Clean) of model and environment
        model.Dispose();
        env.Dispose();
        return fCelu;
    }   
}

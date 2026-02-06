# Minimal Language Project

## Example Code

```rb
# FizzBuzz #

fizzbuzz = (n) {
    for (range(1, 10), (i) {
        if (i.mod(3).equals(0) .and (i.mod(5).equals(0)), () {
            log("FizzBuzz");
        })
        .elseif (i.mod(3).equals(0), () {
            log("Fizz");
        })
        .elseif (i.mod(5).equals(0), () {
            log("Buzz");
        })
        .else (() {
            log(n);
        });
    });
};
```

```rb
# Double Numbers #

numbers = list(1, 2, 3, 4, 5);
for (numbers, (i, number) {
    numbers.set(i, number.mul(2));
});
log(numbers); # [2, 4, 6, 8, 10] #
```

```rb
# Break Loop #

break = {};
try (() {
    for (range(1, 10), (i) {
        for (range(1, 10), (j) {
            if (i.equals(5) .or (j.equals(3)) {
                throw(break);
            });
        });
    });
})
.catch(break, () { });
```

```rb
# Macros #

a = b.and(c);

$macro("&&", (".and(", ")"));
a = b && c;
```

```rb
# Classes #

Cat = {
    meow = () {
        log("Meow");
    };

    call = () {
        return(this().copy());
    };
};

tama = Cat();
tama.meow();
```

```rb
# Static Classes #

Cat = {
    name = "";

    create = (name) {
        this().name = name;
    };

    call = () {
        log("meow");
    };
};

tama = Cat.create("Tama");
log(tama.name); # Tama #
tama(); # meow #
```

```rb
# Current Scope #

number = 5;
log(this(false).get("number")); # 5 #
```

```rb
# Inheritance #

Animal = {
    name = () {
        return("Animal");
    };
};

Cat = {
    components = list(Animal);

    meow = () {
        log("Meow");
    };
};

tama = Cat();
log(tama.name); # Animal #
tama.meow(); # Meow #
```

```rb
# Actors #

a1 = Actor();
a2 = Actor();

resource = "food";

a1.run(() {
    log(resource.append(" 1"));
});
a2.run(() {
    wait();
    log(resource.append(" 2"));
});

log(resource.append(" 3"));
wait();
log(resource.append(" 4"));

##
# food 3
# food 1
# food 4
# food 2
##
```

```rb
# Named Arguments & Default Arguments #

meow = (count = 3) {
    s = "";
    for (range(1, count), (n) {
        s = s.append("meow").append(" ");
    });
    log(s);
};

meow(); # meow meow meow  "
meow(count = 5); # meow meow meow meow meow  #
meow(2); # meow meow  #
```

```rb
# Generators #

# Explicit #
get_numbers = () {
    return({
        components = list(generator);

        number = 0;

        current = () {
            return(number);
        };
        next = () {
            number = number.add(1);
            return(true, number);
        };
    });
};

# Shorthand #
get_numbers = () {
    number = 0;

    return generator.create(() {
        number = number.add(1);
        return(number);
    });
};
```

```rb
# Tuples #

(num1, num2, num3) = (1, 2, 3);
log((num1, num2, num3).mul(2)); # (2, 4, 6)
```

```rb
# Static Typing #

numbers tuple.of(int) = (1, 2, 3);

stringlist (list.of(string)) = list("a");
stringlist.append("b");

num (int, null) = null;
log(num); # null
```

## Special Words

There are no reserved keywords, but these are "special" words:
- `call` - name of sub-object called when object called
- `components` - name of list to search for identifiers (first checks `scope()`, then `this()`, then each of `components` in breadth-first search)

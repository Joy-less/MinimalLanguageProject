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
break = () {}
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
a = b.and(c)

$macro("&&", (".and(", ")"));
a = b && c
```
